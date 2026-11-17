#!/usr/bin/env python3
"""Bind Runtime publication to reviewed exact packages and deployment receipts.

The reviewed manifest hash is the approval anchor, not a GPU attestation.
This gate performs no network requests and never rebuilds a release package.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import shutil


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module


distribution = load('distribution', 'runtime-distribution.py')
core = load('core_candidate', 'release-candidate.py')
require = core.require
CORE_COMMIT = '7b6f75d0047bb87b72094aa910bcea8be2c5e2bc'
CORE_SHA = 'c05e0e09cab60a5bdab0473b8ce9185548ac5c3289f3fa2eab2c448c00e8faf0'
PLATFORMS = ('win-x64', 'ubuntu.24.04-x64')


def receipt_check(receipt, platform, runtime_sha, commit):
    windows = platform == 'win-x64'
    require(receipt['completed'] is True and receipt['sourceTreeDirty'] is False
            and receipt['distributionRuntime'] is True and receipt['interopBoundaries'] is True
            and receipt['extendedLinux'] is False, 'Incomplete or unsuitable deployment receipt')
    require(receipt['validationCommit'] == commit and receipt['sourceCommit'] == CORE_COMMIT
            and receipt['corePackageSha256'] == CORE_SHA and receipt['runtimePackageSha256'] == runtime_sha,
            'Receipt source or exact package mismatch')
    require(receipt['platform'] == ('win-x64' if windows else 'linux-x64')
            and receipt['host'] in (('windows',) if windows else ('wsl2', 'linux-kfd')),
            'Receipt host mismatch')
    expected = core.WORKLOADS | {'AdvancedInteropBoundaries'} | ({'FrameworkPackage'} if windows else set())
    rows = receipt['workloads']
    require(len(rows) == len(expected) and {r['workload'] for r in rows} == expected,
            'Missing or duplicated deployment workload')
    for row in rows:
        if row['status'] == 'skipped-capability':
            require(row['workload'] in ({'ManagedMemory'} if windows else {'ManagedMemory', 'ArrayTextureSurface'})
                    and row['exitCode'] == 0, 'Unapproved capability skip')
        else:
            require(row['status'] == 'passed' and row['exitCode'] ==
                    (1 if row['workload'] == 'MissingHipRtcRejected' else 0), 'Failed deployment workload')


def validate(root, manifest, commit):
    require(re.fullmatch('[0-9a-f]{40}', commit) is not None, 'Invalid source commit')
    inventory = distribution.lock()
    require(manifest['schemaVersion'] == 1 and manifest['scope'] == 'reviewed-runtime-publication'
            and manifest['version'] == '10.1.0' and manifest['sourceCommit'] == commit
            and manifest['inventoryLockSha256'] == distribution.digest(distribution.LOCK_PATH)
            and manifest['sourceBundle'] == inventory['sourceBundle'], 'Publication identity mismatch')
    rows = manifest['files']
    require(len(rows) == 5 and len({r['path'] for r in rows}) == 5, 'Expected five exact review files')
    core.check_bundle_files(root, [r['path'] for r in rows])
    for row in rows:
        path = core.bundle_file(root, row['path'])
        require(path.stat().st_size == row['bytes'] and core.sha256(path) == row['sha256'], 'Review file changed')
    core_path = root/'JYPPX.ROCm.HIP.CSharp.API.1.0.0.nupkg'
    require(core.sha256(core_path) == CORE_SHA and core.package_identity(core_path) ==
            (core.CORE_ID, '1.0.0', CORE_COMMIT), 'Published Core identity mismatch')
    expected_names = {core_path.name}
    for platform in PLATFORMS:
        name = inventory['platforms'][platform]['packageId']+'.10.1.0.nupkg'
        receipt_name = platform+'-deployment.json'
        expected_names.update((name, receipt_name))
        package = root/name
        native = distribution.verify(package, platform, inventory)
        require(native['sourceCommit'] == commit, 'Runtime package source differs from reviewed commit')
        receipt_check(core.read_json(root/receipt_name), platform, core.sha256(package), commit)
    require({r['path'] for r in rows} == expected_names, 'Unexpected review file names')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('seal', 'verify'))
    parser.add_argument('--bundle', type=Path, required=True)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--manifest-sha256')
    args = parser.parse_args(); root = args.bundle.resolve(); path = root/'candidate.json'
    if args.command == 'seal':
        require(not path.exists(), 'Do not overwrite a sealed review')
        inventory = distribution.lock()
        rows = [dict(path=p.name,bytes=p.stat().st_size,sha256=core.sha256(p)) for p in sorted(root.iterdir()) if p.is_file()]
        manifest = dict(schemaVersion=1,scope='reviewed-runtime-publication',version='10.1.0',sourceCommit=args.commit,
                        inventoryLockSha256=distribution.digest(distribution.LOCK_PATH),
                        sourceBundle=inventory['sourceBundle'],files=rows)
        validate(root,manifest,args.commit)
        path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8',newline='\n')
    else:
        require(re.fullmatch('[0-9a-f]{64}', args.manifest_sha256 or '') is not None
                and core.sha256(path) == args.manifest_sha256, 'Reviewed manifest hash mismatch')
        validate(root,core.read_json(path),args.commit)
    print('Reviewed Runtime publication verified: '+core.sha256(path))


if __name__ == '__main__':
    main()
