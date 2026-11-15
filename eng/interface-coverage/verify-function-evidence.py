#!/usr/bin/env python3
"""Check reviewed function receipts offline; full verification also requires recovered evidence."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

REPOSITORY = Path(__file__).resolve().parents[2]
RECEIPTS = Path(__file__).with_name('reviewed-function-evidence.json')
CORE_ID = 'JYPPX.ROCm.HIP.CSharp.API'
REQUIRED_WORKLOADS = {
    'EnvironmentAndDevice', 'LinearMemoryCopy', 'StreamAndEvent', 'HipRtcVectorAdd',
    'HipRtcNegativeCompile', 'HipRtcProgramLinker', 'AsyncVectorAdd', 'PinnedHostMemory',
    'PitchedMemory2D3D', 'ManagedMemory', 'AsyncAllocationAndMemoryPool', 'VirtualMemory',
    'GraphCaptureReplay', 'ExplicitGraphDag', 'KernelOccupancy', 'ModuleGlobals',
    'ArrayTextureSurface', 'HipManagedExpansionValidation', 'PhysicalMemorySharing',
    'CrossProcessIpc', 'PublishedVectorAdd', 'MissingHipRtcRejected', 'RidPublishDotnet', 'RidPublishAppHost',
}
FIXTURES = {
    'CrossProcessIpc': ('eng/local-runtime/IpcProcessSmoke.cs', 'raw-bindings', {
        'hipIpcGetMemHandle', 'hipIpcOpenMemHandle', 'hipIpcCloseMemHandle',
        'hipIpcGetEventHandle', 'hipIpcOpenEventHandle'}),
    'PhysicalMemorySharing': ('eng/local-runtime/PhysicalMemorySharingSmoke.cs', 'managed-owners', {
        'hipMemExportToShareableHandle', 'hipMemImportFromShareableHandle'}),
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=unique_object)


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def relative_path(value):
    require(isinstance(value, str) and value and '\\' not in value and ':' not in value,
            'Expected portable relative evidence path')
    path = PurePosixPath(value)
    require(not path.is_absolute() and all(p not in ('', '.', '..') for p in value.split('/')),
            'Unsafe evidence path: ' + value)
    return path


def checked_file(root, item):
    path = root
    for part in relative_path(item['path']).parts:
        path = path / part
        require(not path.is_symlink() and not (hasattr(path, 'is_junction') and path.is_junction()),
                'Linked evidence is not accepted: ' + str(path))
    require(path.is_file(), 'Missing evidence: ' + str(path))
    require(sha256(path) == item['sha256'], 'Evidence SHA-256 mismatch: ' + item['path'])
    return path


def validate_pin(item):
    require(set(item) == {'path', 'sha256'}, 'Invalid file pin')
    relative_path(item['path'])
    require(re.fullmatch('[0-9a-f]{64}', item['sha256']) is not None, 'Invalid SHA-256')


def validate_inventory(document, repository=REPOSITORY):
    require(set(document) == {'schemaVersion', 'scope', 'groups'}, 'Invalid receipt inventory fields')
    require(type(document['schemaVersion']) is int and document['schemaVersion'] == 1, 'Unsupported receipt schema')
    require(document['scope'] == 'reviewed-exact-package-positive-resource-evidence', 'Invalid inventory scope')
    require(isinstance(document['groups'], list) and document['groups'], 'No reviewed groups')
    model = read_json(repository / 'eng/interop/complete-api-model.json')
    entries = {f['entryPoint'] for f in model['runtimeFunctions'] + model['rtcFunctions']}
    ids, claimed = set(), set()
    for group in document['groups']:
        require(set(group) == {'id', 'workload', 'sourceCommit', 'corePackage', 'fixture',
                               'entries', 'invocation', 'coverage', 'runs'}, 'Invalid group fields')
        require(re.fullmatch('[a-z0-9-]+', group['id']) is not None and group['id'] not in ids,
                'Invalid or duplicate group ID')
        ids.add(group['id'])
        require(re.fullmatch('[0-9a-f]{40}', group['sourceCommit']) is not None, 'Invalid source commit')
        require(group['coverage'] == 'positive-resource-lifetime-only', 'Unreviewed coverage expansion')
        require(group['workload'] in FIXTURES, 'Unreviewed resource fixture')
        fixture, invocation, allowed = FIXTURES[group['workload']]
        require(group['invocation'] == invocation, 'Incorrect raw/managed coverage claim')
        require(group['fixture']['path'] == fixture, 'Incorrect fixture source')
        validate_pin(group['fixture'])
        validate_pin(group['corePackage'])
        require(len(group['entries']) == len(allowed) and set(group['entries']) == allowed,
                'Fixture does not prove the claimed entries')
        require(not claimed.intersection(allowed) and allowed.issubset(entries), 'Duplicate or unknown function claim')
        claimed.update(allowed)
        require(isinstance(group['runs'], list) and len(group['runs']) == 2, 'Both reviewed native closures are required')
        run_ids = set()
        for run in group['runs']:
            require(set(run) == {'id', 'summary', 'log', 'runtimePackage', 'expectedScope'}, 'Invalid run fields')
            require(run['id'] in ('public-7.2.1', 'local-10.1.0') and run['id'] not in run_ids,
                    'Duplicate or unknown runtime run')
            run_ids.add(run['id'])
            expected = 'published-runtime-regression' if run['id'] == 'public-7.2.1' else 'local-package-consumers'
            require(run['expectedScope'] == expected, 'Incorrect runtime scope')
            for name in ('summary', 'log', 'runtimePackage'):
                validate_pin(run[name])
    return claimed


def package_identity(path):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), 'Duplicate package entries')
        nuspecs = [n for n in names if n.endswith('.nuspec')]
        require(len(nuspecs) == 1, 'Expected one package nuspec')
        root = ET.fromstring(archive.read(nuspecs[0]))
        metadata = next((e for e in root if e.tag.split('}')[-1] == 'metadata'), None)
        require(metadata is not None, 'Missing package metadata')
        values = {e.tag.split('}')[-1]: e for e in metadata}
        return (values['id'].text, values['version'].text, values['repository'].get('commit') if 'repository' in values else None)


def verify_log(group, text):
    audits = [json.loads(line.split('=', 1)[1], object_pairs_hook=unique_object)
              for line in text.splitlines() if line.startswith('PACKAGE_MODULE_AUDIT=')]
    minimum = 2 if group['workload'] == 'CrossProcessIpc' else 1
    require(len(audits) == minimum and all(a.get('passed') is True and isinstance(a.get('modules'), list) and
            a['modules'] and all(isinstance(p, str) and p.startswith('/') and '/runtimes/linux-x64/native/' in p for p in a['modules'])
            for a in audits),
            'Missing successful module audit for each process')
    if group['workload'] == 'CrossProcessIpc':
        calls = re.findall(r'^IPC_API api=([^;]+); code=(\d+); pid=(\d+)$', text, re.MULTILINE)
        require(all(code == '0' for _, code, _ in calls), 'IPC native call failed')
        require(set(group['entries']).issubset({name for name, _, _ in calls}), 'Missing IPC function execution')
        by_api = {name: pid for name, _, pid in calls}
        require(by_api['hipIpcGetMemHandle'] == by_api['hipIpcGetEventHandle'] and
                by_api['hipIpcOpenMemHandle'] == by_api['hipIpcOpenEventHandle'] == by_api['hipIpcCloseMemHandle'] and
                by_api['hipIpcGetMemHandle'] != by_api['hipIpcOpenMemHandle'], 'IPC did not use separate owner/importer processes')
        require('IPC_CHILD=passed; by-value-handles=64; byteComparison=4096' in text and
                'IPC_PARENT=passed; separateProcess=true; memory=true; event=true; parentOwnerAfterChildClose=true' in text,
                'Missing IPC byte/lifetime assertions')
    else:
        require('PHYSICAL_SHARING=passed; sharedBytes=true; independentImportRelease=true; originalOwnerAlive=true' in text,
                'Missing physical sharing/lifetime assertions')
        require('POSIX_EXPORT_ZERO=accepted' in text, 'Descriptor-zero export was not exercised')
        require('POSIX_FD_ZERO=native-InvalidValue-propagated' in text, 'Missing reviewed native fd-zero limitation')


def verify(document, workspace, repository=REPOSITORY):
    claimed = validate_inventory(document, repository)
    workspace = workspace.resolve()
    verified = []
    for group in document['groups']:
        # Read the immutable fixture at the tested commit, not an edited current checkout.
        fixture = subprocess.run(['git', '-C', str(repository), 'show',
                                  group['sourceCommit'] + ':' + group['fixture']['path']],
                                 capture_output=True, check=True).stdout
        require(hashlib.sha256(fixture.replace(b'\r\n', b'\n')).hexdigest() == group['fixture']['sha256'],
                'Tested fixture source hash mismatch')
        core = checked_file(workspace, group['corePackage'])
        require(package_identity(core) == (CORE_ID, '1.0.0', group['sourceCommit']), 'Core package identity mismatch')
        for run in group['runs']:
            summary = read_json(checked_file(workspace, run['summary']))
            require(summary['sourceCommit'] == summary['validationCommit'] == group['sourceCommit'], 'Test source commit mismatch')
            require(summary['sourceTreeDirty'] is False and summary['completed'] is True, 'Incomplete or dirty validation')
            require(summary['scope'] == run['expectedScope'] and summary['host'] == 'linux-kfd' and
                    summary['platform'] == 'linux-x64' and summary['architecture'] == 'gfx1100' and
                    summary['extendedLinux'] is True, 'Incorrect GPU validation scope')
            require(summary['corePackageSha256'] == group['corePackage']['sha256'] and
                    summary['runtimePackageSha256'] == run['runtimePackage']['sha256'], 'Validation package hash mismatch')
            workloads = summary['workloads']
            require(len(workloads) == 24 and {w['workload'] for w in workloads} == REQUIRED_WORKLOADS and
                    all(w['status'] == 'passed' and w['exitCode'] == (1 if w['workload'] == 'MissingHipRtcRejected' else 0) for w in workloads),
                    'Incomplete, duplicate, skipped or failed workload')
            require(sum(w['workload'] == group['workload'] for w in workloads) == 1, 'Missing resource workload')
            runtime = checked_file(workspace, run['runtimePackage'])
            runtime_id, version, _ = package_identity(runtime)
            expected_version = '7.2.1' if run['id'] == 'public-7.2.1' else '10.1.0-local.20261113.3'
            require((runtime_id, version) == (CORE_ID + '.Runtime.ubuntu.24.04-x64', expected_version), 'Runtime package identity mismatch')
            verify_log(group, checked_file(workspace, run['log']).read_text(encoding='utf-8-sig'))
            verified.append({'group': group['id'], 'runtime': run['id'], 'sourceCommit': group['sourceCommit']})
    return {'scope': document['scope'], 'functionCount': len(claimed), 'runs': verified,
            'currentHeadValidated': False, 'fullNegativeCoverage': False, 'publicationApproved': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--receipts', type=Path, default=RECEIPTS)
    parser.add_argument('--workspace', type=Path, help='Verify recovered logs and exact packages; never downloads missing evidence')
    parser.add_argument('--output', type=Path, help='Optional report outside the source repository')
    args = parser.parse_args()
    document = read_json(args.receipts)
    if args.workspace:
        report = verify(document, args.workspace)
    else:
        report = {'scope': 'inventory-only-no-execution-proof', 'functions': len(validate_inventory(document)), 'executionVerified': False}
    serialized = json.dumps(report, indent=2) + '\n'
    if args.output:
        require(not args.output.resolve().is_relative_to(REPOSITORY), 'Report output must be outside source')
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(serialized, encoding='utf-8')
    print(serialized, end='')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, ET.ParseError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print('Function evidence rejected: ' + str(error), file=sys.stderr)
        sys.exit(1)
