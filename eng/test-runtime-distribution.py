"""Boundary checks for pinned Runtime payload and redistribution metadata."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import warnings
import zipfile

spec = importlib.util.spec_from_file_location('distribution', Path(__file__).with_name('runtime-distribution.py'))
gate = importlib.util.module_from_spec(spec); spec.loader.exec_module(gate)


class DistributionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name)/'fixture.nupkg'
        native = b'synthetic-native-fixture'; sha = hashlib.sha256(native).hexdigest()
        notice = b'synthetic-license-fixture'; license_sha = hashlib.sha256(notice).hexdigest()
        self.inventory = dict(version='10.1.0',notices=[dict(sha256=license_sha)],sourceBundle=dict(sha256='a'*64,url='https://example.invalid/source.zip'),platforms={
            'win-x64':dict(packageId='fixture.runtime',rid='win-x64',licenseAcceptance=True,
                           files=[dict(name='fixture.dll',sha256=sha,bytes=len(native))])})
        manifest = dict(scope='runtime-distribution-candidate',packageId='fixture.runtime',packageVersion='10.1.0',rid='win-x64',
            inventoryLockSha256=gate.digest(gate.LOCK_PATH),sourceCommit='b'*40,sourceTreeDirty=False,
            files=[dict(name='fixture.dll',packagePath='runtimes/win-x64/native/fixture.dll',sha256=sha)])
        self.files = {
            'THIRD-PARTY-NOTICES.txt':b'Individual licenses apply.',
            'README.md':b'Synthetic fixture only.',
            'SOURCE-BUILD-INSTRUCTIONS.md':b'Synthetic source instructions.',
            'runtimes/win-x64/native/fixture.dll':native,
            'runtime-distribution-manifest.json':json.dumps(manifest).encode(),
            'source-provision.json':json.dumps(self.inventory['sourceBundle']).encode(),
            'licenses/source-'+license_sha[:20]+'.txt':notice,
            'runtime.cdx.json':json.dumps(dict(bomFormat='CycloneDX',components=[dict(name='fixture.dll',hashes=[dict(content=sha)])])).encode(),
            'fixture.runtime.nuspec':b'<package><metadata><id>fixture.runtime</id><version>10.1.0</version><license type="file">THIRD-PARTY-NOTICES.txt</license><requireLicenseAcceptance>true</requireLicenseAcceptance><repository commit="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"/></metadata></package>'}

    def write(self, files=None, extra=None):
        files = copy.deepcopy(files or self.files)
        files['distribution-files.json'] = json.dumps([dict(path=n,sha256=hashlib.sha256(v).hexdigest())
            for n,v in files.items() if not n.endswith('.nuspec')]).encode()
        with zipfile.ZipFile(self.path,'w') as archive:
            for name,data in files.items(): archive.writestr(name,data)
            for name,data in (extra or []): archive.writestr(name,data)

    def verify(self):
        return gate.verify(self.path,'win-x64',self.inventory)

    def test_synthetic_fixture_only(self):
        self.write(); self.assertEqual(self.verify()['scope'],'runtime-distribution-candidate')

    def test_native_hash_cannot_be_relabelled_by_manifest(self):
        files = copy.deepcopy(self.files); files['runtimes/win-x64/native/fixture.dll']=b'changed'
        self.write(files)
        with self.assertRaisesRegex(ValueError,'Native bytes mismatch'): self.verify()

    def test_extra_native_rejected(self):
        files=copy.deepcopy(self.files);files['runtimes/win-x64/native/unlisted.dll']=b'extra';self.write(files)
        with self.assertRaisesRegex(ValueError,'Native payload'):self.verify()

    def test_unlisted_non_native_rejected(self):
        self.write(extra=[('install.ps1',b'unlisted')])
        with self.assertRaisesRegex(ValueError,'Unexpected package entry'):self.verify()

    def test_indexed_build_script_rejected(self):
        files=copy.deepcopy(self.files);files['build/fixture.runtime.targets']=b'build hook';self.write(files)
        with self.assertRaisesRegex(ValueError,'Unexpected package entry'):self.verify()

    def test_metadata_directory_does_not_hide_extra_payload(self):
        self.write(extra=[('package/services/metadata/hidden.dll',b'unlisted')])
        with self.assertRaisesRegex(ValueError,'Unexpected package entry'):self.verify()

    def test_notice_content_is_fixed_independently_of_index(self):
        files=copy.deepcopy(self.files);name=next(n for n in files if n.startswith('licenses/'));files[name]=b'changed';self.write(files)
        with self.assertRaisesRegex(ValueError,'License text changed'):self.verify()

    def test_missing_notice_rejected(self):
        files={n:v for n,v in self.files.items() if not n.startswith('licenses/')};self.write(files)
        with self.assertRaisesRegex(ValueError,'Notice inventory'):self.verify()

    def test_source_offer_url_cannot_be_changed(self):
        files=copy.deepcopy(self.files);files['source-provision.json']=b'{"url":"https://example.invalid/changed"}';self.write(files)
        with self.assertRaisesRegex(ValueError,'Source provision'):self.verify()

    def test_duplicate_archive_entry_rejected(self):
        with warnings.catch_warnings():
            warnings.simplefilter('ignore'); self.write(extra=[('runtime.cdx.json',self.files['runtime.cdx.json'])])
        with self.assertRaisesRegex(ValueError,'Duplicate ZIP'):self.verify()

    def test_duplicate_json_rejected(self):
        with self.assertRaisesRegex(ValueError,'Duplicate JSON'):gate.unique_json('{"name":1,"name":2}')

    def test_path_traversal_and_windows_paths_rejected(self):
        for name in ('../outside','/outside','C:/outside','a\\outside','a//b','a/./b',
                     'a/NUL.dll','a/trailing.','a/space ','a/<bad>'):
            with self.subTest(name=name),self.assertRaises(ValueError):gate.safe_name(name)

    def test_declared_license_acceptance_cannot_be_dropped(self):
        files=copy.deepcopy(self.files);files['fixture.runtime.nuspec']=files['fixture.runtime.nuspec'].replace(b'>true<',b'>false<');self.write(files)
        with self.assertRaisesRegex(ValueError,'License acceptance'):self.verify()

    def test_injected_dependency_rejected(self):
        files=copy.deepcopy(self.files);files['fixture.runtime.nuspec']=files['fixture.runtime.nuspec'].replace(b'</metadata>',b'<dependencies/></metadata>');self.write(files)
        with self.assertRaisesRegex(ValueError,'Unexpected dependency'):self.verify()

    def test_sbom_relabelled_hash_rejected(self):
        files=copy.deepcopy(self.files);files['runtime.cdx.json']=files['runtime.cdx.json'].replace(b'fixture.dll',b'other.dll');self.write(files)
        with self.assertRaisesRegex(ValueError,'SBOM native'):self.verify()

    def test_dirty_source_not_a_distribution_candidate(self):
        files=copy.deepcopy(self.files);m=json.loads(files['runtime-distribution-manifest.json']);m['sourceTreeDirty']=True;files['runtime-distribution-manifest.json']=json.dumps(m).encode();self.write(files)
        with self.assertRaisesRegex(ValueError,'Repository source'):self.verify()

    def test_truncated_pe_rejected(self):
        self.path.write_bytes(b'not a PE file')
        with self.assertRaises(ValueError):gate.pe_imports(self.path)

    def test_signature_child_does_not_inherit_powershell7_modules(self):
        with mock.patch.dict(gate.os.environ, {'PSMODULEPATH':'synthetic-pwsh7-modules'}), \
             mock.patch.object(gate, 'pe_imports', return_value=[]), \
             mock.patch.object(gate.subprocess, 'run') as run:
            run.return_value.returncode=0
            run.return_value.stdout='[{"name":"fixture.dll","status":"Valid"}]'
            gate.windows_audit(self.path.parent,self.inventory['platforms']['win-x64'])
            self.assertFalse(any(k.upper()=='PSMODULEPATH' for k in run.call_args.kwargs['env']))

    def test_wrong_source_bundle_hash_rejected_before_opening(self):
        self.path.write_bytes(b'not a source bundle')
        with self.assertRaisesRegex(ValueError,'Source bundle'):gate.source_materials(self.path,dict(sourceBundle=dict(bytes=19,sha256='a'*64)))


if __name__=='__main__':unittest.main(verbosity=2)
