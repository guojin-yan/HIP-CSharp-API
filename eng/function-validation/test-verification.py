#!/usr/bin/env python3
import copy
import importlib.util
import json
import os
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

    def host_fixture(self):
        source = self.repository / "eng/function-validation/host-kernel.hip"
        source.parent.mkdir(parents=True)
        source.write_text("registered test kernel", encoding="utf-8")
        self.summary["toolSources"]["eng/function-validation/host-kernel.hip"] = sha(source)
        compiler = self.root / "hipcc"
        compiler.write_bytes(b"test compiler")
        helper = self.output / "host-kernel.so"
        helper.write_bytes(b"test registered helper")
        deployed = self.output / "bin/Release/net10.0/host-kernel.so"
        deployed.parent.mkdir(parents=True)
        deployed.write_bytes(helper.read_bytes())
        build_log = self.output / "helper-build.log"
        build_log.write_text("compiled", encoding="utf-8")
        self.summary["nativeHelper"] = dict(file=helper.name, sha256=sha(helper), compiler=str(compiler), compilerSha256=sha(compiler),
                                           source="eng/function-validation/host-kernel.hip", sourceSha256=sha(source),
                                           log=build_log.name, logSha256=sha(build_log))
        result = self.summary["results"][0]
        result.update(fixture="host-runtime", log="host-runtime.log")
        log = self.log.read_text(encoding="utf-8").replace("device", "host-runtime")
        marker = "NATIVE_HELPER=" + json.dumps(dict(path=str(deployed), sha256=sha(helper), symbol="ValidationKernel"))
        self.log = self.output / result["log"]
        self.log.write_text(log + "\n" + marker, encoding="utf-8")
        self.rehash_host()

    def rehash_host(self):
        self.summary["results"][0]["logSha256"] = sha(self.log)
        self.summary["functions"] = inventory(self.summary["results"], {"Raw.Init": "hipInit", **{
            "Raw.Function" + str(i): "hipFunction" + str(i) for i in range(476)}})

    def test_host_helper_binding(self):
        self.host_fixture()
        self.assertTrue(self.verify()["integrityVerified"])

    def test_host_missing_build_identity(self):
        self.host_fixture(); del self.summary["nativeHelper"]
        with self.assertRaises(ValueError): self.verify()

    def test_host_missing_marker(self):
        self.host_fixture()
        self.log.write_text(self.log.read_text().split("\nNATIVE_HELPER=")[0], encoding="utf-8")
        self.rehash_host()
        with self.assertRaises(ValueError): self.verify()

    def test_host_duplicate_marker(self):
        self.host_fixture()
        log = self.log.read_text()
        self.log.write_text(log + "\n" + log.splitlines()[-1], encoding="utf-8")
        self.rehash_host()
        with self.assertRaises(ValueError): self.verify()

    def test_host_wrong_symbol(self):
        self.host_fixture()
        self.log.write_text(self.log.read_text().replace("ValidationKernel", "ValidationSymbol"), encoding="utf-8")
        self.rehash_host()
        with self.assertRaises(ValueError): self.verify()

    def test_host_wrong_loaded_path(self):
        self.host_fixture()
        self.log.write_text(self.log.read_text().replace("host-kernel.so", "another-helper.so"), encoding="utf-8")
        self.rehash_host()
        with self.assertRaises(ValueError): self.verify()

    def test_host_changed_deployed_binary(self):
        self.host_fixture()
        (self.output / "bin/Release/net10.0/host-kernel.so").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.verify()

    def test_host_changed_build_binary(self):
        self.host_fixture(); (self.output / "host-kernel.so").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.verify()

    def test_host_changed_build_log(self):
        self.host_fixture(); (self.output / "helper-build.log").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.verify()

    def test_host_changed_compiler(self):
        self.host_fixture(); (self.root / "hipcc").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.verify()

    def test_host_unbound_source(self):
        self.host_fixture(); self.summary["nativeHelper"]["sourceSha256"] = "0" * 64
        with self.assertRaises(ValueError): self.verify()

    def test_host_source_path_escape(self):
        self.host_fixture(); self.summary["nativeHelper"]["source"] = "../host-kernel.hip"
        with self.assertRaises(ValueError): self.verify()

    def test_relative_summary_resolves_loaded_helper(self):
        self.host_fixture(); self.verify()
        previous = Path.cwd()
        try:
            os.chdir(self.root)
            self.assertTrue(module.verify(Path("output/summary.json"), repository=self.repository)["integrityVerified"])
        finally: os.chdir(previous)

    def fatbinary_fixture(self):
        self.host_fixture()
        payload = self.output / "host-kernel.fatbin"
        payload.write_bytes(b"test fatbinary")
        deployed = self.output / "bin/Release/net10.0/host-kernel.fatbin"
        deployed.write_bytes(payload.read_bytes())
        object_file = self.output / "host-kernel.o"
        object_file.write_bytes(b"test compiled object")
        extractor = self.root / "llvm-objcopy"
        extractor.write_bytes(b"test extractor")
        self.summary["nativeHelper"]["fatbinary"] = dict(file=payload.name, sha256=sha(payload), objectFile=object_file.name,
                                                           objectSha256=sha(object_file), extractor=str(extractor), extractorSha256=sha(extractor))
        result = self.summary["results"][0]
        result.update(fixture="module-texture-array", log="module-texture-array.log")
        log = self.log.read_text().split("\nNATIVE_HELPER=")[0].replace("host-runtime", result["fixture"])
        self.log = self.output / result["log"]
        self.log.write_text(log + "\nNATIVE_FATBIN=" + json.dumps(dict(path=str(deployed), sha256=sha(payload))), encoding="utf-8")
        self.rehash_host()

    def test_fatbinary_binding(self):
        self.fatbinary_fixture(); self.assertTrue(self.verify()["integrityVerified"])

    def test_fatbinary_missing_identity(self):
        self.fatbinary_fixture(); del self.summary["nativeHelper"]["fatbinary"]
        with self.assertRaises(ValueError): self.verify()

    def test_fatbinary_deployed_tampering(self):
        self.fatbinary_fixture(); (self.output / "bin/Release/net10.0/host-kernel.fatbin").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.verify()

    def test_fatbinary_built_tampering(self):
        self.fatbinary_fixture(); (self.output / "host-kernel.fatbin").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.verify()

    def test_fatbinary_object_tampering(self):
        self.fatbinary_fixture(); (self.output / "host-kernel.o").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.verify()

    def test_fatbinary_extractor_tampering(self):
        self.fatbinary_fixture(); (self.root / "llvm-objcopy").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.verify()

    def test_fatbinary_missing_marker(self):
        self.fatbinary_fixture()
        self.log.write_text(self.log.read_text().split("\nNATIVE_FATBIN=")[0], encoding="utf-8"); self.rehash_host()
        with self.assertRaises(ValueError): self.verify()

    def test_fatbinary_duplicate_marker(self):
        self.fatbinary_fixture(); log = self.log.read_text()
        self.log.write_text(log + "\n" + log.splitlines()[-1], encoding="utf-8"); self.rehash_host()
        with self.assertRaises(ValueError): self.verify()

    def test_fatbinary_path_escape(self):
        self.fatbinary_fixture(); self.summary["nativeHelper"]["fatbinary"]["objectFile"] = "../host-kernel.o"
        with self.assertRaises(ValueError): self.verify()


if __name__ == "__main__":
    unittest.main()
