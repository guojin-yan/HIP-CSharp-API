#!/usr/bin/env python3
import copy
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile

sys.dont_write_bytecode = True
from evidence import inventory
from run import sha

spec = importlib.util.spec_from_file_location("fixture_verify", Path(__file__).with_name("verify.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ArtifactIntegrityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.repository = self.root / "source"
        self.repository.mkdir()
        self.output = self.root / "output"
        self.output.mkdir()
        model_path = self.repository / "eng/interop/complete-api-model.json"
        model_path.parent.mkdir(parents=True)
        functions = [dict(managedName="Init", entryPoint="hipInit")]
        functions += [dict(managedName="Function" + str(i), entryPoint="hipFunction" + str(i)) for i in range(476)]
        model_path.write_text(json.dumps(dict(runtimeFunctions=functions, rtcFunctions=[])), encoding="utf-8")
        self.core = self.root / "core.nupkg"
        with zipfile.ZipFile(self.core, "w") as package:
            package.writestr("core.nuspec", '<package><metadata><repository commit="' + "a" * 40 + '"/></metadata></package>')
        self.runtime = self.root / "runtime.nupkg"
        self.runtime.write_bytes(b"exact runtime bytes")
        call = dict(expression="Raw.Init(0)", result=0, expected=0, phase="positive")
        receipt = dict(fixture="device", passed=True, assertionsCompleted=True, unsupported=[], calls=[call])
        audit = dict(passed=True, modules=["/consumer/libamdhip64.so"])
        self.log = self.output / "device.log"
        self.log.write_text("\n".join(("FIXTURE_START=device", "NATIVE_CALL=" + json.dumps(call),
                                       "FUNCTION_FIXTURE=" + json.dumps(receipt), "PACKAGE_MODULE_AUDIT=" + json.dumps(audit))), encoding="utf-8")
        result = dict(fixture="device", passed=True, exitCode=0, log="device.log", logSha256=sha(self.log),
                      functions={"hipInit": ["positive"]}, nativeRestrictions=[])
        self.summary = dict(scope="local-native-function-fixtures", completed=True, evidenceStable=True, validationCommit="b" * 40,
                            sourceCommit="a" * 40, sourceTreeDirty=False, fullMatrix=False, allFunctionsSupported=True, allPassed=True,
                            toolSources={"eng/interop/complete-api-model.json": sha(model_path)},
                            core=dict(path=str(self.core), sha256=sha(self.core)), runtime=dict(path=str(self.runtime), sha256=sha(self.runtime)),
                            results=[result], functions=inventory([result], {"Raw." + f["managedName"]: f["entryPoint"] for f in functions}))
        self.path = self.output / "summary.json"

    def verify(self, summary=None):
        self.path.write_text(json.dumps(summary or self.summary), encoding="utf-8")
        return module.verify(self.path, repository=self.repository)

    def test_exact_artifact_recomputation(self):
        self.assertEqual(self.verify()["positiveFunctions"], 1)

    def test_modified_package(self):
        self.core.write_bytes(b"replaced")
        with self.assertRaises(ValueError): self.verify()

    def test_modified_runtime(self):
        self.runtime.write_bytes(b"replaced")
        with self.assertRaises(ValueError): self.verify()

    def test_missing_execution_log(self):
        self.log.unlink()
        with self.assertRaises(FileNotFoundError): self.verify()

    def test_changed_log(self):
        self.log.write_text("truncated")
        with self.assertRaises(ValueError): self.verify()

    def test_changed_tool_source(self):
        (self.repository / "eng/interop/complete-api-model.json").write_text("{}")
        with self.assertRaises(ValueError): self.verify()

    def test_stale_source_commit(self):
        self.summary["sourceCommit"] = "c" * 40
        with self.assertRaises(ValueError): self.verify()

    def test_omitted_inventory_row(self):
        self.summary["functions"].pop()
        with self.assertRaises(ValueError): self.verify()

    def test_claimed_extra_positive(self):
        self.summary["functions"][0]["positiveFixtures"] = ["device"]
        with self.assertRaises(ValueError): self.verify()

    def test_claimed_full_matrix(self):
        self.summary["fullMatrix"] = True
        with self.assertRaises(ValueError): self.verify()

    def test_duplicate_fixture(self):
        self.summary["results"].append(copy.deepcopy(self.summary["results"][0]))
        with self.assertRaises(ValueError): self.verify()

    def test_log_path_escape(self):
        self.summary["results"][0]["log"] = "../device.log"
        with self.assertRaises(ValueError): self.verify()

    def test_unstable_tool_execution(self):
        self.summary["evidenceStable"] = False
        with self.assertRaises(ValueError): self.verify()

    def test_fabricated_passed_outcome(self):
        self.summary["allPassed"] = False
        with self.assertRaises(ValueError): self.verify()

    def test_derived_fixture_functions_tampered(self):
        self.summary["results"][0]["functions"] = {}
        with self.assertRaises(ValueError): self.verify()


if __name__ == "__main__":
    unittest.main()
