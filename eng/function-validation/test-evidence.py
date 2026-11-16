#!/usr/bin/env python3
import copy
import json
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).parent))
from evidence import parse


class EvidenceBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.call = dict(expression="Raw.Init(0)", result=0, expected=0, phase="positive")
        self.receipt = dict(fixture="device", passed=True, assertionsCompleted=True, unsupported=[], calls=[self.call])
        self.audit = dict(passed=True, modules=["/consumer/libamdhip64.so"])

    def log(self, receipt=None, call=None, audit=None):
        return "\n".join(("FIXTURE_START=device", "NATIVE_CALL=" + json.dumps(call or self.call),
                          "FUNCTION_FIXTURE=" + json.dumps(receipt or self.receipt),
                          "PACKAGE_MODULE_AUDIT=" + json.dumps(audit or self.audit)))

    def parse(self, log):
        return parse(log, 0, "device", {"Raw.Init": "hipInit"})

    def test_completed_execution(self):
        self.assertEqual(self.parse(self.log())["functions"], {"hipInit": ["positive"]})

    def test_nonzero_exit_never_counts_earlier_success(self):
        self.assertFalse(parse(self.log(), 1, "device", {"Raw.Init": "hipInit"})["passed"])

    def test_truncated_execution(self):
        with self.assertRaises(ValueError): self.parse(self.log().split("FUNCTION_FIXTURE=")[0])

    def test_missing_assertions(self):
        receipt = copy.deepcopy(self.receipt); receipt["assertionsCompleted"] = False
        with self.assertRaises(ValueError): self.parse(self.log(receipt=receipt))

    def test_trace_receipt_mismatch(self):
        receipt = copy.deepcopy(self.receipt); receipt["calls"].append(self.call)
        with self.assertRaises(ValueError): self.parse(self.log(receipt=receipt))

    def test_failed_module_audit(self):
        with self.assertRaises(ValueError): self.parse(self.log(audit=dict(passed=False, modules=["/consumer/libamdhip64.so"])))

    def test_empty_module_audit(self):
        with self.assertRaises(ValueError): self.parse(self.log(audit=dict(passed=True, modules=[])))

    def test_duplicate_completion(self):
        with self.assertRaises(ValueError): self.parse(self.log() + "\nFUNCTION_FIXTURE=" + json.dumps(self.receipt))

    def test_mismatched_fixture(self):
        with self.assertRaises(ValueError): self.parse(self.log().replace("FIXTURE_START=device", "FIXTURE_START=context"))

    def test_duplicate_json_key(self):
        with self.assertRaises(ValueError): self.parse(self.log().replace('"passed": true', '"passed": false, "passed": true'))

    def mutate_call(self, **values):
        call = dict(self.call, **values); receipt = dict(self.receipt, calls=[call])
        return self.log(receipt=receipt, call=call)

    def test_native_failure_as_success(self):
        with self.assertRaises(ValueError): self.parse(self.mutate_call(result=1, expected=1))

    def test_mismatched_return(self):
        with self.assertRaises(ValueError): self.parse(self.mutate_call(result=1))

    def test_boolean_not_status_integer(self):
        with self.assertRaises(ValueError): self.parse(self.mutate_call(result=False, expected=False))

    def test_unknown_entry(self):
        with self.assertRaises(ValueError): self.parse(self.mutate_call(expression="Raw.MadeUp()"))

    def test_unsupported_not_positive(self):
        call = dict(self.call, result=801, expected=801, phase="native-not-supported")
        receipt = dict(self.receipt, calls=[call], unsupported=[call["expression"]])
        result = self.parse(self.log(receipt=receipt, call=call))
        self.assertEqual(result["functions"], {})
        self.assertEqual(len(result["nativeRestrictions"]), 1)

    def test_invalid_error_not_unsupported(self):
        with self.assertRaises(ValueError): self.parse(self.mutate_call(result=1, expected=1, phase="native-not-supported"))

    def test_unsupported_inventory_missing(self):
        with self.assertRaises(ValueError): self.parse(self.mutate_call(result=801, expected=801, phase="native-not-supported"))

    def test_empty_returned_text(self):
        with self.assertRaises(ValueError): self.parse(self.mutate_call(result="", expected="", returnKind="validated-text"))

    def test_native_negative_preserved(self):
        result = self.parse(self.mutate_call(result=1, expected=1, phase="negative"))
        self.assertEqual(result["functions"], {"hipInit": ["negative"]})


if __name__ == "__main__":
    unittest.main()
