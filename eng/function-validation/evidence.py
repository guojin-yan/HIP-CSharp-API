"""Fail-closed validation of a single isolated native fixture's execution log."""
import json
import re

HOST_SYMBOLS = {"host-runtime": ("ValidationKernel",), "host-attributes": ("ValidationKernel",), "host-symbol": ("ValidationSymbol",),
                "registered-texture-parameters": ("RegisteredTexture",), "registered-texture-array": ("RegisteredTexture", "RegisteredTextureKernel"),
                "registered-texture-linear": ("RegisteredTexture",), "registered-texture-pitched": ("RegisteredTexture", "RegisteredTextureKernel"),
                "registered-texture-mipmapped": ("RegisteredTexture", "RegisteredTextureKernel"), "legacy-device-properties": ("ValidationKernel",)}
FATBIN_FIXTURES = ("module-texture-array", "module-texture-linear", "module-texture-pitched", "module-texture-mipmapped")


def validate_fatbinary(log, path, digest):
    records = [json.loads(line[len("NATIVE_FATBIN="):], object_pairs_hook=unique_object)
               for line in log.splitlines() if line.startswith("NATIVE_FATBIN=")]
    if records != [dict(path=str(path), sha256=digest)]:
        raise ValueError("Missing, duplicate, or mismatched native fatbinary marker")


def validate_helper(log, fixture, helper_path, helper_hash, restricted=False):
    records = [json.loads(line[len("NATIVE_HELPER="):], object_pairs_hook=unique_object)
               for line in log.splitlines() if line.startswith("NATIVE_HELPER=")]
    expected = [dict(path=str(helper_path), sha256=helper_hash, symbol=symbol) for symbol in HOST_SYMBOLS[fixture]]
    if restricted and fixture in ("registered-texture-array", "registered-texture-pitched", "registered-texture-mipmapped"):
        expected = expected[:1]
    if records != expected:
        raise ValueError("Missing, duplicate, or mismatched registered native helper marker")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON key: " + key)
        result[key] = value
    return result


def parse(log, exit_code, fixture, mapping):
    if exit_code != 0:
        return dict(passed=False, functions={}, nativeRestrictions=[])

    def records(prefix):
        return [json.loads(line[len(prefix):], object_pairs_hook=unique_object)
                for line in log.splitlines() if line.startswith(prefix)]

    starts = [line for line in log.splitlines() if line.startswith("FIXTURE_START=")]
    receipts = records("FUNCTION_FIXTURE=")
    audits = records("PACKAGE_MODULE_AUDIT=")
    traces = records("NATIVE_CALL=")
    if starts != ["FIXTURE_START=" + fixture] or len(receipts) != 1 or len(audits) != 1:
        raise ValueError("Missing, duplicate, or mismatched fixture/audit markers")
    receipt, audit = receipts[0], audits[0]
    if receipt.get("fixture") != fixture or receipt.get("passed") is not True or receipt.get("assertionsCompleted") is not True:
        raise ValueError("Missing completed assertions")
    if audit.get("passed") is not True or not isinstance(audit.get("modules"), list) or not audit["modules"]:
        raise ValueError("Missing native module audit")
    if not all(isinstance(path, str) and path.strip() for path in audit["modules"]):
        raise ValueError("Invalid audited module path")
    if not traces or receipt.get("calls") != traces:
        raise ValueError("Receipt does not match the ordered execution trace")
    functions, restrictions = {}, []
    for call in traces:
        expression = call.get("expression", "")
        match = re.match(r"((?:Raw|Rtc)\.\w+)\(", expression)
        if not match or match[1] not in mapping:
            raise ValueError("Unknown native function expression")
        phase = call.get("phase")
        if phase not in ("positive", "negative", "native-not-supported") or call.get("result") != call.get("expected"):
            raise ValueError("Unexpected native result or phase")
        if call.get("returnKind") == "validated-text":
            if phase != "positive" or not isinstance(call.get("result"), str) or not call["result"].strip():
                raise ValueError("Missing validated native text")
        elif call.get("returnKind") == "validated-scalar":
            if phase != "positive" or type(call.get("result")) is not int or type(call.get("expected")) is not int:
                raise ValueError("Missing validated native scalar")
        else:
            if type(call.get("result")) is not int or type(call.get("expected")) is not int:
                raise ValueError("Native status must be an integer")
            if (phase == "positive" and call["result"] != 0) or (phase == "negative" and call["result"] == 0):
                raise ValueError("Native success/error classified incorrectly")
            if phase == "native-not-supported" and call["result"] != 801:
                raise ValueError("Only NotSupported may be recorded as a restriction")
        if phase == "native-not-supported":
            restrictions.append(expression)
        else:
            functions.setdefault(mapping[match[1]], set()).add(phase)
    if receipt.get("unsupported", []) != restrictions:
        raise ValueError("Native restriction inventory mismatch")
    return dict(passed=True, functions={k: sorted(v) for k, v in sorted(functions.items())}, nativeRestrictions=restrictions)


def inventory(results, mapping):
    rows = []
    for entry in sorted(set(mapping.values())):
        positive = [r["fixture"] for r in results if "positive" in r["functions"].get(entry, [])]
        negative = [r["fixture"] for r in results if "negative" in r["functions"].get(entry, [])]
        restrictions = [r["fixture"] for r in results for expression in r["nativeRestrictions"]
                        if mapping[re.match(r"((?:Raw|Rtc)\.\w+)\(", expression)[1]] == entry]
        rows.append(dict(entryPoint=entry, positiveFixtures=positive, negativeFixtures=negative, restrictionFixtures=restrictions,
                         status="positive-and-negative" if positive and negative else "positive" if positive else
                         "negative-only" if negative else "native-not-supported" if restrictions else "not-validated"))
    return rows
