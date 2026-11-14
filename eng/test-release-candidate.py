#!/usr/bin/env python3
"""Mutation regressions for the release trust boundary; no native SDK is needed."""
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import warnings
import zipfile

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("candidate", Path(__file__).with_name("release-candidate.py"))
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)
COMMIT = "a" * 40
VERSION = "0.11.0"


class CandidateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.files = {}
        self.core = "core.nupkg"
        self.package(self.core, gate.CORE_ID)
        self.validations = {}
        for rid in ("win-x64", "linux-x64"):
            runtime = rid + ".nupkg"
            self.package(runtime, gate.CORE_ID + ".Runtime." +
                         ("win-x64" if rid == "win-x64" else "ubuntu.24.04-x64"))
            required = gate.WORKLOADS | ({"FrameworkPackage"} if rid == "win-x64" else set())
            summary = dict(scope="local-package-consumers", platform=rid, architecture="gfx1100",
                           host="windows" if rid == "win-x64" else "wsl2", completed=True,
                           sourceCommit=COMMIT, validationCommit=COMMIT, sourceTreeDirty=False,
                           corePackageSha256=self.files[self.core], runtimePackageSha256=self.files[runtime],
                           workloads=[dict(workload=n, status="passed", exitCode=1 if n == "MissingHipRtcRejected" else 0)
                                      for n in sorted(required)])
            summary_name = rid + ".json"
            self.write(summary_name, json.dumps(summary))
            self.validations[rid] = dict(summary=summary_name, runtimePackage=runtime)
        self.write("evidence.txt", "Reviewed source, ABI, API, docs and support evidence")
        self.manifest = dict(schemaVersion=1, version=VERSION, sourceCommit=COMMIT, corePackage=self.core,
                             files=self.files, validations=self.validations,
                             reviewEvidence={r: "evidence.txt" for r in ("managed", "api", "abi", "docs", "support")})

    def write(self, name, text):
        (self.root / name).write_text(text, encoding="utf-8")
        self.files[name] = gate.sha256(self.root / name)

    def package(self, name, package_id):
        with zipfile.ZipFile(self.root / name, "w") as z:
            z.writestr("core.nuspec", f'<package><metadata><id>{package_id}</id><version>{VERSION}</version>'
                       f'<repository commit="{COMMIT}"/></metadata></package>')
            for tfm in gate.TFMS:
                for ext in ("dll", "xml"):
                    z.writestr(f"lib/{tfm}/{gate.CORE_ID}.{ext}", "fixture " + tfm)
            z.writestr("README.md", "reviewed README")
        self.files[name] = gate.sha256(self.root / name)

    def validate(self):
        return gate.validate(self.root, self.manifest, COMMIT, VERSION)

    def change_summary(self, action):
        name = "win-x64.json"
        summary = json.loads((self.root / name).read_text())
        action(summary)
        self.write(name, json.dumps(summary))

    def test_complete_candidate(self):
        self.assertEqual(self.validate(), self.core)

    def test_tampered_package_rejected(self):
        with (self.root / self.core).open("ab") as f:
            f.write(b"tampered")
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            self.validate()

    def test_tampered_evidence_rejected(self):
        (self.root / "evidence.txt").write_text("unreviewed")
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            self.validate()

    def test_unlisted_file_rejected(self):
        (self.root / "unreviewed.dll").write_bytes(b"unreviewed")
        with self.assertRaisesRegex(ValueError, "unlisted files"):
            self.validate()

    def test_manifest_not_part_of_own_payload(self):
        self.write("candidate.json", "{}")
        with self.assertRaisesRegex(ValueError, "cannot hash itself"):
            self.validate()

    def test_duplicate_json_keys_rejected(self):
        path = self.root / "win-x64.json"
        original = path.read_text()
        # Even identical duplicate fields are ambiguous across JSON consumers.
        path.write_text(original[:-1] + ', "completed": true}', encoding="utf-8")
        self.files[path.name] = gate.sha256(path)
        with self.assertRaisesRegex(ValueError, "Duplicate JSON key"):
            self.validate()

    def test_non_object_summary_rejected(self):
        self.write("win-x64.json", "[]")
        with self.assertRaisesRegex(ValueError, "summary must be an object"):
            self.validate()

    def test_linked_bundle_entry_rejected(self):
        if os.name == "nt":
            self.skipTest("Linux exercises symlinks; Windows junction has a separate regression")
        path = self.root / "evidence.txt"
        path.unlink()
        path.symlink_to(self.root / "win-x64.json")
        self.files[path.name] = gate.sha256(path)
        with self.assertRaisesRegex(ValueError, "Linked candidate entry"):
            self.validate()

    @unittest.skipUnless(os.name == "nt", "Windows junction semantics")
    def test_windows_junction_rejected(self):
        import subprocess
        target = self.root / "payload"
        target.mkdir()
        link = self.root / "alias"
        subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(target)], check=True, capture_output=True)
        try:
            with self.assertRaisesRegex(ValueError, "Linked candidate entry"):
                self.validate()
        finally:
            link.rmdir()

    def test_wrong_source_rejected(self):
        self.change_summary(lambda s: s.update(validationCommit="b" * 40))
        with self.assertRaisesRegex(ValueError, "source commit mismatch"):
            self.validate()

    def test_dirty_worktree_rejected(self):
        self.change_summary(lambda s: s.update(sourceTreeDirty=True))
        with self.assertRaisesRegex(ValueError, "clean worktree"):
            self.validate()

    def test_missing_flat_publish_rejected(self):
        self.change_summary(lambda s: s.update(workloads=[w for w in s["workloads"] if w["workload"] != "RidPublishAppHost"]))
        with self.assertRaisesRegex(ValueError, "Missing or duplicate"):
            self.validate()

    def test_skipped_required_deployment_rejected(self):
        self.change_summary(lambda s: next(w for w in s["workloads"] if w["workload"] == "FrameworkPackage").update(status="skipped-capability"))
        with self.assertRaisesRegex(ValueError, "cannot be skipped"):
            self.validate()

    def test_failed_negative_rejected(self):
        self.change_summary(lambda s: next(w for w in s["workloads"] if w["workload"] == "MissingHipRtcRejected").update(exitCode=0))
        with self.assertRaisesRegex(ValueError, "Failed workload"):
            self.validate()

    def test_unhashed_and_escaping_file_rejected(self):
        self.manifest["corePackage"] = "other.nupkg"
        with self.assertRaisesRegex(ValueError, "not hashed"):
            self.validate()
        self.manifest["files"]["../escape"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "Unsafe relative path"):
            self.validate()

    def test_missing_review_role_rejected(self):
        del self.manifest["reviewEvidence"]["abi"]
        with self.assertRaisesRegex(ValueError, "evidence roles"):
            self.validate()

    def test_signed_reordered_container_matches(self):
        target = self.root / "signed.nupkg"
        with zipfile.ZipFile(self.root / self.core) as original, zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as signed:
            for name in reversed(original.namelist()):
                signed.writestr(name, original.read(name))
            signed.writestr(".signature.p7s", "signature verification belongs to dotnet nuget verify")
        gate.compare(self.root / self.core, target)

    def test_changed_nuspec_or_readme_rejected(self):
        for change in ("core.nuspec", "README.md"):
            with self.subTest(change=change):
                target = self.root / "changed.nupkg"
                with zipfile.ZipFile(self.root / self.core) as original, zipfile.ZipFile(target, "w") as altered:
                    for name in original.namelist():
                        altered.writestr(name, original.read(name) + (b"changed" if name == change else b""))
                with self.assertRaisesRegex(ValueError, "payload differs"):
                    gate.compare(self.root / self.core, target)

    def test_duplicate_and_unsafe_zip_entries_rejected(self):
        for names in (("README.md", "readme.md"), ("README.md", "README.md"), ("../escape",), ("C:/escape",)):
            with self.subTest(names=names):
                target = self.root / "invalid.nupkg"
                with warnings.catch_warnings(), zipfile.ZipFile(target, "w") as z:
                    warnings.simplefilter("ignore", UserWarning)
                    for name in names:
                        z.writestr(name, "fixture")
                with self.assertRaises(ValueError):
                    gate.package_entries(target)

    def test_archive_extracts_only_after_hash_check(self):
        target = self.root / "bundle.zip"
        with zipfile.ZipFile(target, "w") as z:
            z.writestr("nested/candidate.json", "fixture")
        output = self.root / "extracted"
        with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
            gate.unpack(target, "0" * 64, output)
        self.assertFalse(output.exists())
        gate.unpack(target, gate.sha256(target), output)
        self.assertEqual((output / "nested/candidate.json").read_text(), "fixture")

    def test_windows_reserved_and_illegal_names_rejected(self):
        for name in ("CON", "dir/nul.txt", "COM1.dll", "LPT³.log", "x:stream", "x?y", "x|y", "x<y", "x\x01y", "x.", "x "):
            with self.subTest(name=name), self.assertRaises(ValueError):
                gate.safe_name(name)

    def test_archive_parent_collisions_rejected_before_extraction(self):
        for names in (("lib", "lib/core.dll"), ("Lib/a.dll", "lib/b.dll")):
            with self.subTest(names=names):
                target = self.root / "collision.zip"
                with zipfile.ZipFile(target, "w") as z:
                    for name in names:
                        z.writestr(name, "fixture")
                output = self.root / "extracted"
                with self.assertRaisesRegex(ValueError, "Conflicting archive path"):
                    gate.unpack(target, gate.sha256(target), output)
                self.assertFalse(output.exists())

    def test_archive_traversal_rejected_before_extraction(self):
        target = self.root / "escape.zip"
        with zipfile.ZipFile(target, "w") as z:
            z.writestr("../outside", "fixture")
        output = self.root / "extracted"
        with self.assertRaises(ValueError):
            gate.unpack(target, gate.sha256(target), output)
        self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main(verbosity=2)
