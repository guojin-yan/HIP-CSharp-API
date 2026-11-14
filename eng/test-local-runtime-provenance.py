#!/usr/bin/env python3
"""Mutation tests for archive-to-installed-binary provenance validation."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
import warnings
import zipfile

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("audit", Path(__file__).with_name("audit-local-runtime-provenance.py"))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class ProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.sdk = self.root / "sdk"
        manifest = self.sdk / "share/therock/therock_manifest.json"
        manifest.parent.mkdir(parents=True)
        manifest.write_text(json.dumps({"the_rock_commit": "a" * 40}))
        library = self.sdk / "bin/library.dll"
        library.parent.mkdir()
        library.write_bytes(b"reviewed native binary")
        self.wheel = self.root / "core.whl"
        self.inventory = {"sdkManifestSha256": audit.digest(manifest), "files": [
            {"name": "library.dll", "source": "bin/library.dll", "sha256": audit.digest(library)}]}
        with zipfile.ZipFile(self.wheel, "w") as z:
            z.write(manifest, "_core/share/therock/therock_manifest.json")
            z.write(library, "_core/bin/library.dll")
        self.item = {"bytes": self.wheel.stat().st_size, "sha256": audit.digest(self.wheel),
                     "sdkManifestMember": "_core/share/therock/therock_manifest.json", "files": [
                         {"name": "library.dll", "member": "_core/bin/library.dll", "sha256": audit.digest(library)}]}

    def verify(self):
        return audit.verify_wheel(self.wheel, self.sdk, self.item, self.inventory)

    def test_valid_mapping(self):
        self.assertEqual(self.verify()["the_rock_commit"], "a" * 40)

    def test_archive_mutation_rejected(self):
        with self.wheel.open("ab") as stream:
            stream.write(b"tampered")
        with self.assertRaisesRegex(ValueError, "Wheel hash/size"):
            self.verify()

    def test_installed_binary_mutation_rejected(self):
        (self.sdk / "bin/library.dll").write_bytes(b"other binary")
        with self.assertRaisesRegex(ValueError, "Installed binary differs"):
            self.verify()

    def test_manifest_mutation_rejected(self):
        (self.sdk / "share/therock/therock_manifest.json").write_text("{}")
        with self.assertRaisesRegex(ValueError, "SDK manifest mismatch"):
            self.verify()

    def test_missing_mapping_rejected(self):
        self.item["files"] = []
        with self.assertRaisesRegex(ValueError, "Incomplete or duplicate"):
            self.verify()

    def test_duplicate_archive_entry_rejected(self):
        with warnings.catch_warnings(), zipfile.ZipFile(self.wheel, "a") as z:
            warnings.simplefilter("ignore", UserWarning)
            z.writestr("_core/bin/library.dll", "changed")
        self.item.update(bytes=self.wheel.stat().st_size, sha256=audit.digest(self.wheel))
        with self.assertRaisesRegex(ValueError, "Duplicate wheel entry"):
            self.verify()

    def test_wrong_member_hash_rejected(self):
        self.item["files"][0]["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "Inventory/mapping hash mismatch"):
            self.verify()

    def test_traversing_member_rejected(self):
        self.item["files"][0]["member"] = "../library.dll"
        with self.assertRaisesRegex(ValueError, "Invalid wheel member"):
            self.verify()


if __name__ == "__main__":
    unittest.main(verbosity=2)
