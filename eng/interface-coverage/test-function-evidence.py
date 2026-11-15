#!/usr/bin/env python3
"""Offline receipt-boundary tests. Synthetic logs are fixtures, never GPU evidence."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('function_evidence', Path(__file__).with_name('verify-function-evidence.py'))
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)


class ReceiptTests(unittest.TestCase):
    def setUp(self):
        output = Path(os.environ.get('HIPSHARP_OUTPUT_ROOT', tempfile.gettempdir())).resolve()
        evidence.require(not output.is_relative_to(evidence.REPOSITORY), 'Test output must be outside source')
        output.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='function-evidence-', dir=output)
        self.addCleanup(self.temp.cleanup)
        self.workspace = Path(self.temp.name)
        self.document = copy.deepcopy(evidence.read_json(evidence.RECEIPTS))
        self.group = self.document['groups'][0]
        self.group['runs'] = copy.deepcopy(self.group['runs'])
        self.document['groups'] = [self.group]
        self.group['corePackage'] = self.package('core.nupkg', evidence.CORE_ID, '1.0.0', self.group['sourceCommit'])
        for run in self.group['runs']:
            version = '7.2.1' if run['id'] == 'public-7.2.1' else '10.1.0-local.20261113.3'
            run['runtimePackage'] = self.package(run['id'] + '.nupkg', evidence.CORE_ID + '.Runtime.ubuntu.24.04-x64', version)
            summary = dict(scope=run['expectedScope'], host='linux-kfd', platform='linux-x64', architecture='gfx1100',
                           extendedLinux=True, sourceCommit=self.group['sourceCommit'], validationCommit=self.group['sourceCommit'],
                           sourceTreeDirty=False, completed=True, corePackageSha256=self.group['corePackage']['sha256'],
                           runtimePackageSha256=run['runtimePackage']['sha256'],
                           workloads=[dict(workload=name, status='passed', exitCode=1 if name == 'MissingHipRtcRejected' else 0)
                                      for name in sorted(evidence.REQUIRED_WORKLOADS)])
            run['summary'] = self.write(run['id'] + '/summary.json', json.dumps(summary))
            log = '\n'.join('IPC_API api=' + name + '; code=0; pid=' + ('101' if 'Get' in name else '202')
                            for name in self.group['entries'])
            log += '\nIPC_CHILD=passed; by-value-handles=64; byteComparison=4096\n'
            log += 'IPC_PARENT=passed; separateProcess=true; memory=true; event=true; parentOwnerAfterChildClose=true\n'
            log += 'PACKAGE_MODULE_AUDIT={"passed":true,"modules":["/test-only/runtimes/linux-x64/native/libamdhip64.so"]}\n' * 2
            run['log'] = self.write(run['id'] + '/run.log', log)

    def write(self, path, text):
        target = self.workspace / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding='utf-8')
        return {'path': path, 'sha256': evidence.sha256(target)}

    def package(self, path, name, version, commit=None):
        with zipfile.ZipFile(self.workspace / path, 'w') as archive:
            repository = '<repository commit="' + commit + '"/>' if commit else ''
            archive.writestr('test.nuspec', f'<package><metadata><id>{name}</id><version>{version}</version>{repository}</metadata></package>')
        return {'path': path, 'sha256': evidence.sha256(self.workspace / path)}

    def summary(self, mutate):
        run = self.group['runs'][0]
        data = evidence.read_json(self.workspace / run['summary']['path'])
        mutate(data)
        run['summary'] = self.write(run['summary']['path'], json.dumps(data))

    def log(self, mutate):
        run = self.group['runs'][0]
        path = self.workspace / run['log']['path']
        run['log'] = self.write(run['log']['path'], mutate(path.read_text(encoding='utf-8')))

    def reject(self):
        with self.assertRaises(ValueError):
            evidence.verify(self.document, self.workspace)

    def test_tracked_inventory_has_seven_reviewed_functions(self):
        self.assertEqual(7, len(evidence.validate_inventory(evidence.read_json(evidence.RECEIPTS))))

    def test_synthetic_receipt_can_verify_without_claiming_current_head(self):
        report = evidence.verify(self.document, self.workspace)
        self.assertEqual(5, report['functionCount'])
        self.assertFalse(report['currentHeadValidated'])
        self.assertFalse(report['fullNegativeCoverage'])
        self.assertFalse(report['publicationApproved'])

    def test_rejects_changed_package_bytes(self):
        with (self.workspace / 'core.nupkg').open('ab') as stream:
            stream.write(b'changed')
        self.reject()

    def test_rejects_rehashed_package_with_wrong_source(self):
        self.group['corePackage'] = self.package('core.nupkg', evidence.CORE_ID, '1.0.0', '0' * 40)
        self.reject()

    def test_rejects_rehashed_wrong_runtime_identity(self):
        run = self.group['runs'][0]
        run['runtimePackage'] = self.package('wrong.nupkg', evidence.CORE_ID + '.Runtime.win-x64', '7.2.1')
        self.summary(lambda s: s.update(runtimePackageSha256=run['runtimePackage']['sha256']))
        self.reject()

    def test_rejects_edited_fixture_pin(self):
        self.group['fixture']['sha256'] = '0' * 64
        self.reject()

    def test_rejects_missing_evidence(self):
        (self.workspace / self.group['runs'][0]['log']['path']).unlink()
        self.reject()

    def test_rejects_dirty_incomplete_wrong_commit_or_scope(self):
        for change in [dict(sourceTreeDirty=True), dict(completed=False), dict(validationCommit='0' * 40),
                       dict(host='wsl2'), dict(architecture='gfx9999'), dict(scope='published-runtime-compiler-only')]:
            with self.subTest(change=change):
                original = copy.deepcopy(self.document)
                self.summary(lambda s: s.update(change))
                self.reject()
                self.document = original
                self.group = self.document['groups'][0]
                # Restore the original bytes as well as the pin for the next mutation.
                run = self.group['runs'][0]
                data = evidence.read_json(self.workspace / run['summary']['path'])
                defaults = dict(sourceTreeDirty=False, completed=True, validationCommit=self.group['sourceCommit'],
                                host='linux-kfd', architecture='gfx1100', scope=run['expectedScope'])
                data.update(defaults)
                self.write(run['summary']['path'], json.dumps(data))

    def test_rejects_skipped_or_duplicate_workloads(self):
        self.summary(lambda s: s['workloads'][0].update(status='skipped-capability'))
        self.reject()

    def test_rejects_duplicate_workload_replacing_required_case(self):
        self.summary(lambda s: s['workloads'].__setitem__(0, s['workloads'][1]))
        self.reject()

    def test_rejects_same_process_ipc(self):
        self.log(lambda s: s.replace('pid=202', 'pid=101'))
        self.reject()

    def test_rejects_missing_close_or_lifetime_assertion(self):
        self.log(lambda s: '\n'.join(line for line in s.splitlines() if 'api=hipIpcCloseMemHandle;' not in line))
        self.reject()

    def test_rejects_native_error(self):
        self.log(lambda s: s.replace('code=0', 'code=1', 1))
        self.reject()

    def test_rejects_missing_child_module_audit(self):
        self.log(lambda s: s.replace('PACKAGE_MODULE_AUDIT=', 'IGNORED=', 1))
        self.reject()

    def test_rejects_malformed_module_audit(self):
        self.log(lambda s: s.replace('["/test-only/runtimes/linux-x64/native/libamdhip64.so"]', '"not-a-module-list"'))
        self.reject()

    def test_rejects_boolean_schema_version(self):
        self.document['schemaVersion'] = True
        self.reject()

    def test_rejects_unsupported_function_claim_or_duplicate_group(self):
        self.group['entries'].append('hipImportExternalMemory')
        self.reject()

    def test_rejects_duplicate_group(self):
        self.document['groups'].append(copy.deepcopy(self.group))
        self.reject()

    def test_rejects_path_escape(self):
        for path in ('../outside.log', '/outside.log', 'C:/outside.log', 'folder/../outside.log', 'folder\\file.log'):
            with self.subTest(path=path), self.assertRaises(ValueError):
                evidence.relative_path(path)

    def test_rejects_duplicate_json_keys(self):
        pin = self.write('duplicate.json', '{"completed":true,"completed":false}')
        with self.assertRaises(ValueError):
            evidence.read_json(self.workspace / pin['path'])

    def use_physical_fixture(self):
        original = evidence.read_json(evidence.RECEIPTS)['groups'][1]
        for key in ('id', 'workload', 'fixture', 'entries', 'invocation'):
            self.group[key] = copy.deepcopy(original[key])
        for run in self.group['runs']:
            text = 'POSIX_FD_ZERO=native-InvalidValue-propagated\nPOSIX_EXPORT_ZERO=accepted\n'
            text += 'PHYSICAL_SHARING=passed; sharedBytes=true; independentImportRelease=true; originalOwnerAlive=true\n'
            text += 'PACKAGE_MODULE_AUDIT={"passed":true,"modules":["/test-only/runtimes/linux-x64/native/libamdhip64.so"]}\n'
            run['log'] = self.write(run['log']['path'], text)

    def test_physical_fixture_records_native_fd_zero_rejection(self):
        self.use_physical_fixture()
        self.assertEqual(2, evidence.verify(self.document, self.workspace)['functionCount'])

    def test_rejects_removed_physical_sharing_lifetime_assertion(self):
        self.use_physical_fixture()
        self.log(lambda s: s.replace('originalOwnerAlive=true', 'originalOwnerAlive=false'))
        self.reject()

    def test_rejects_relabelled_native_fd_zero_import(self):
        self.use_physical_fixture()
        self.log(lambda s: s.replace('native-InvalidValue-propagated', 'native-accepted'))
        self.reject()

    def test_rejects_linked_evidence(self):
        run = self.group['runs'][0]
        original = self.workspace / run['log']['path']
        linked = self.workspace / 'linked.log'
        try:
            linked.symlink_to(original)
        except OSError as error:
            self.skipTest('Creating symlinks is unavailable: ' + str(error))
        run['log']['path'] = 'linked.log'
        self.reject()


if __name__ == '__main__':
    unittest.main()
