#!/usr/bin/env python3
"""Pack and audit the pinned official 10.1.0 closure; no downloads or private patches."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path, PurePosixPath
import shutil
import struct
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET
import zipfile

REPOSITORY = Path(__file__).resolve().parent.parent
LOCK_PATH = REPOSITORY / 'eng/runtime-distribution-10.1.0.lock.json'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def unique_json(data):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, 'Duplicate JSON field: ' + key)
            result[key] = value
        return result
    return json.loads(data, object_pairs_hook=pairs)


def lock():
    return unique_json(LOCK_PATH.read_text(encoding='utf-8'))


def safe_name(name):
    path = PurePosixPath(name)
    require(name and '\\' not in name and ':' not in name and not path.is_absolute()
            and '..' not in path.parts and '.' not in path.parts
            and str(path) == name, 'Unsafe archive path: ' + name)
    for part in path.parts:
        require(not re.search(r'[<>"|?*\x00-\x1f]', part) and not part.endswith((' ', '.'))
                and part.split('.')[0].upper() not in
                {'CON', 'PRN', 'AUX', 'NUL', *('COM'+str(i) for i in range(1, 10)),
                 *('LPT'+str(i) for i in range(1, 10))}, 'Unsafe Windows archive path: ' + name)
    return name


def checked_archive(archive):
    names = archive.namelist()
    require(len(names) == len(set(names)), 'Duplicate ZIP entry')
    require(len(names) == len({name.casefold() for name in names}), 'Case-conflicting ZIP entry')
    for name in names:
        safe_name(name)
    return set(names)


def source_materials(path, inventory):
    expected = inventory['sourceBundle']
    require(path.stat().st_size == expected['bytes'] and digest(path) == expected['sha256'],
            'Source bundle hash/size mismatch')
    notices = {}
    with zipfile.ZipFile(path) as archive:
        names = checked_archive(archive)
        require('README.md' in names and 'packaging-reference.py' in names, 'Missing source/build instructions')
        for item in inventory['sourceArchives']:
            name = safe_name(item['path'])
            with archive.open(name) as stream:
                require(hashlib.file_digest(stream, 'sha256').hexdigest() == item['sha256'],
                        'Corresponding source archive mismatch: ' + name)
        for item in inventory['notices']:
            data = archive.read(safe_name(item['path']))
            require(hashlib.sha256(data).hexdigest() == item['sha256'], 'Notice hash mismatch')
            notices['licenses/source-' + item['sha256'][:20] + '.txt'] = data
        instructions = archive.read('README.md')
    require(any('elfutils' in i['path'] for i in inventory['sourceArchives'])
            and any('numactl' in i['path'] for i in inventory['sourceArchives']),
            'LGPL corresponding source archives missing')
    return notices, instructions


def pe_imports(path):
    data = path.read_bytes()
    require(len(data) >= 64 and data[:2] == b'MZ', 'Not a PE file')
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    require(pe+264 <= len(data), 'Truncated PE header')
    require(data[pe:pe+4] == b'PE\0\0', 'Invalid PE signature')
    machine, count = struct.unpack_from('<HH', data, pe+4)
    opt_size = struct.unpack_from('<H', data, pe+20)[0]
    opt = pe+24
    require(machine == 0x8664 and struct.unpack_from('<H', data, opt)[0] == 0x20b,
            'Only x64 PE32+ binaries are allowed')
    require(0 < count <= 96 and opt_size >= 240, 'Invalid PE sections')
    require(opt+opt_size+count*40 <= len(data), 'Truncated PE section table')
    sections = [struct.unpack_from('<IIII', data, opt+opt_size+i*40+8) for i in range(count)]
    def offset(rva):
        for virtual_size, virtual_address, raw_size, raw_offset in sections:
            if virtual_address <= rva < virtual_address + max(virtual_size, raw_size):
                delta = rva-virtual_address
                require(delta < raw_size and raw_offset+delta < len(data), 'Invalid PE import RVA')
                return raw_offset+delta
        raise ValueError('Unmapped PE RVA')
    imports = []
    for index, width, name_index in ((1, 20, 3), (13, 32, 1)):
        rva = struct.unpack_from('<I', data, opt+112+index*8)[0]
        if not rva:
            continue
        base = offset(rva)
        for step in range(4096):
            require(base+(step+1)*width <= len(data), 'Truncated PE import table')
            row = struct.unpack_from('<'+'I'*(width//4), data, base+step*width)
            if not any(row):
                break
            require(index != 13 or row[0] == 1, 'Unsupported delay import address format')
            start = offset(row[name_index]); end = data.find(b'\0', start, start+512)
            require(end != -1, 'Invalid PE import name')
            imports.append(data[start:end].decode('ascii').lower())
        else:
            raise ValueError('Unterminated PE imports')
    return sorted(set(imports))


def windows_audit(directory, platform):
    names = {item['name'].lower() for item in platform['files']}
    system = set(('advapi32 bcrypt cfgmgr32 combase crypt32 dbghelp dxgi d3d12 gdi32 '
                  'kernel32 msvcp_win ole32 oleaut32 rpcrt4 sechost setupapi shell32 shlwapi '
                  'ucrtbase user32 userenv version winmm ws2_32 ntdll msvcp140 vcruntime140 '
                  'vcruntime140_1').split())
    system = {name+'.dll' for name in system}
    imports = {}
    for item in platform['files']:
        imports[item['name']] = pe_imports(directory/item['name'])
        for dependency in imports[item['name']]:
            require(dependency in names or dependency in system
                    or dependency.startswith(('api-ms-win-', 'ext-ms-win-')),
                    'Unresolved non-system PE import: ' + dependency)
    script = r'''$ErrorActionPreference='Stop';
    $rows = @(Get-ChildItem -LiteralPath $env:HIPSHARP_SIGNATURE_DIRECTORY -Filter *.dll | ForEach-Object {
      $s=Get-AuthenticodeSignature -LiteralPath $_.FullName;
      if($s.Status -ne 'Valid' -or $null -eq $s.SignerCertificate -or
         -not $s.SignerCertificate.Subject.Contains('Advanced Micro Devices')) { throw "Invalid AMD signature: $($_.Name)" }
      [pscustomobject]@{name=$_.Name;status=[string]$s.Status;signer=$s.SignerCertificate.Subject;thumbprint=$s.SignerCertificate.Thumbprint}
    }); ConvertTo-Json -InputObject $rows -Depth 4'''
    env = os.environ.copy(); env['HIPSHARP_SIGNATURE_DIRECTORY'] = str(directory)
    # PowerShell 7's inherited module path can prevent Windows PowerShell 5.1
    # from loading its built-in Security module.
    env.pop('PSModulePath', None)
    result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command', script],
                            check=True, capture_output=True, text=True, env=env)
    signatures = unique_json(result.stdout)
    require(len(signatures) == len(names), 'Incomplete signature audit')
    return {'imports': imports, 'authenticode': signatures}


def verify(package, platform_name, inventory=None):
    inventory = inventory or lock()
    platform = inventory['platforms'][platform_name]
    with zipfile.ZipFile(package) as archive:
        names = checked_archive(archive)
        require({'THIRD-PARTY-NOTICES.txt','README.md','SOURCE-BUILD-INSTRUCTIONS.md',
                 'runtime.cdx.json','source-provision.json','distribution-files.json'} <= names,
                'Required redistribution documentation missing')
        manifest = unique_json(archive.read('runtime-distribution-manifest.json'))
        require(manifest['scope'] == 'runtime-distribution-candidate'
                and manifest['packageId'] == platform['packageId']
                and manifest['packageVersion'] == inventory['version']
                and manifest['rid'] == platform['rid']
                and manifest['inventoryLockSha256'] == digest(LOCK_PATH), 'Distribution identity mismatch')
        expected = {'runtimes/'+platform['rid']+'/native/'+f['name']: f for f in platform['files']}
        require({n for n in names if n.startswith('runtimes/')} == expected.keys(), 'Native payload differs from lock')
        notice_names = {'licenses/source-'+i['sha256'][:20]+'.txt': i['sha256'] for i in inventory['notices']}
        fixed = {'THIRD-PARTY-NOTICES.txt', 'README.md', 'SOURCE-BUILD-INSTRUCTIONS.md',
                 'runtime.cdx.json', 'source-provision.json', 'distribution-files.json',
                 'runtime-distribution-manifest.json', '[Content_Types].xml', '_rels/.rels', '.signature.p7s',
                 platform['packageId']+'.nuspec'}
        require(all(n in fixed or n in expected or n in notice_names or
                    re.fullmatch(r'package/services/metadata/core-properties/[0-9a-f]+\.psmdcp', n)
                    for n in names), 'Unexpected package entry')
        require(len(manifest['files']) == len(expected), 'Manifest file count mismatch')
        require({f['packagePath'] for f in manifest['files']} == expected.keys(), 'Duplicate or missing manifest native path')
        for item in manifest['files']:
            path = item['packagePath']
            require(path in expected and item['name'] == expected[path]['name']
                    and item['sha256'] == expected[path]['sha256'], 'Manifest native mapping mismatch')
        for name, item in expected.items():
            data = archive.read(name)
            require(len(data) == item['bytes'] and hashlib.sha256(data).hexdigest() == item['sha256'],
                    'Native bytes mismatch: ' + name)
        index = unique_json(archive.read('distribution-files.json'))
        require(len(index) == len({row['path'] for row in index}), 'Duplicate distribution file')
        payload = {row['path'] for row in index}
        require(payload == names - {n for n in names if n.endswith('.nuspec') or n.startswith('_rels/')
                                   or n.startswith('package/services/metadata/') or n in ('[Content_Types].xml', '.signature.p7s', 'distribution-files.json')},
                'Unlisted package entry')
        for row in index:
            require(hashlib.sha256(archive.read(safe_name(row['path']))).hexdigest() == row['sha256'],
                    'Distribution file hash mismatch')
        require({n for n in names if n.startswith('licenses/')} == notice_names.keys(), 'Notice inventory mismatch')
        for name, sha in notice_names.items():
            require(hashlib.sha256(archive.read(name)).hexdigest() == sha, 'License text changed')
        require(unique_json(archive.read('source-provision.json')) == inventory['sourceBundle'], 'Source provision mismatch')
        specs = [n for n in names if n.endswith('.nuspec')]; require(len(specs) == 1, 'Expected one nuspec')
        metadata = ET.fromstring(archive.read(specs[0])).find('{*}metadata')
        require(metadata is not None and metadata.findtext('{*}id') == platform['packageId']
                and metadata.findtext('{*}version') == inventory['version'], 'Nuspec identity mismatch')
        require(metadata.find('{*}dependencies') is None and metadata.findtext('{*}license') == 'THIRD-PARTY-NOTICES.txt',
                'Unexpected dependency or license metadata')
        require(metadata.findtext('{*}requireLicenseAcceptance', 'false').lower()
                == str(platform['licenseAcceptance']).lower(), 'License acceptance policy mismatch')
        repository = metadata.find('{*}repository')
        require(re.fullmatch('[0-9a-f]{40}', manifest['sourceCommit']) is not None
                and manifest['sourceTreeDirty'] is False and repository is not None
                and repository.attrib.get('commit') == manifest['sourceCommit'], 'Repository source binding mismatch')
        sbom = unique_json(archive.read('runtime.cdx.json'))
        require(sbom['bomFormat'] == 'CycloneDX' and len(sbom['components']) == len(expected), 'SBOM count mismatch')
        require({(c['name'], c['hashes'][0]['content']) for c in sbom['components']}
                == {(f['name'],f['sha256']) for f in platform['files']}, 'SBOM native identity mismatch')
    return manifest


def pack(binary_archive, source_bundle, output, platform_name):
    inventory = lock(); platform = inventory['platforms'][platform_name]
    require((platform_name == 'win-x64') == (sys.platform == 'win32'), 'Build on the target host for native audit')
    if sys.platform == 'linux':
        release = Path('/etc/os-release').read_text()
        require('ID=ubuntu\n' in release and 'VERSION_ID="24.04"' in release, 'Ubuntu 24.04 required')
    output = output.resolve()
    require(not output.is_relative_to(REPOSITORY) and not output.exists(), 'Use a new output outside source')
    require(binary_archive.stat().st_size == platform['archive']['bytes']
            and digest(binary_archive) == platform['archive']['sha256'], 'Official binary archive hash/size mismatch')
    notices, instructions = source_materials(source_bundle, inventory)
    commit = subprocess.check_output(['git', '-C', str(REPOSITORY), 'rev-parse', 'HEAD'], text=True).strip()
    require(not subprocess.check_output(['git', '-C', str(REPOSITORY), 'status', '--porcelain'], text=True).strip(),
            'Distribution packing requires a clean source checkout')
    output.mkdir(parents=True)
    stage = output/'stage'; native = stage/'runtimes'/platform['rid']/'native'; native.mkdir(parents=True)
    wanted = {f['archiveMember']: f for f in platform['files'] if 'archiveMember' in f}
    extracted = set()
    def take(name, stream):
        require(name not in extracted, 'Duplicate official archive member')
        item = wanted[name]; data = stream.read()
        require(hashlib.sha256(data).hexdigest() == item['sourceSha256'], 'Official member hash mismatch')
        (native/item['name']).write_bytes(data); extracted.add(name)
    if platform['archiveFormat'] == 'tar':
        with tarfile.open(binary_archive) as archive:
            for member in archive:
                if member.name in wanted:
                    require(member.isfile(), 'Expected regular binary file')
                    with archive.extractfile(member) as stream:
                        take(member.name, stream)
    else:
        with zipfile.ZipFile(binary_archive) as archive:
            require(len(archive.namelist()) == len(set(archive.namelist())), 'Duplicate wheel entry')
            for name in wanted:
                with archive.open(name) as stream:
                    take(name, stream)
    require(extracted == wanted.keys(), 'Missing official archive member')
    for item in platform['files']:
        path = native/item['name']
        if item.get('transformation'):
            subprocess.run(['patchelf', '--force-rpath', '--set-rpath', '$ORIGIN', str(path)], check=True)
        if item.get('aliasOf'):
            shutil.copyfile(native/item['aliasOf'], path)
        require(path.stat().st_size == item['bytes'] and digest(path) == item['sha256'], 'Staged native identity mismatch')
    if platform_name == 'win-x64':
        native_audit = windows_audit(native, platform)
    else:
        env = {k:v for k,v in os.environ.items() if k not in ('HIP_PATH', 'ROCM_PATH', 'LD_LIBRARY_PATH', 'LD_PRELOAD')}
        dependencies = {}
        for path in native.iterdir():
            result = subprocess.run(['ldd', str(path)], env=env, check=True, capture_output=True, text=True).stdout
            require('not found' not in result and '/opt/' not in result, 'ELF closure leaks SDK or missing dependency')
            dependencies[path.name] = result
        native_audit = {'ldd': dependencies}
    files = [{**item, 'packagePath':'runtimes/'+platform['rid']+'/native/'+item['name']} for item in platform['files']]
    manifest = dict(schemaVersion=1,scope='runtime-distribution-candidate',packageId=platform['packageId'],
                    packageVersion=inventory['version'],rid=platform['rid'],sourceCommit=commit,sourceTreeDirty=False,
                    inventoryLockSha256=digest(LOCK_PATH),files=files,archive=platform['archive'],
                    sourceBundle=inventory['sourceBundle'],nativeAudit=native_audit,hostPrerequisites=platform['hostPrerequisites'],
                    sourceNotes=platform['sourceNotes'],gpuValidation='Separate exact-package deployment receipts required')
    for name, data in notices.items():
        path = stage/name; path.parent.mkdir(exist_ok=True); path.write_bytes(data)
    (stage/'SOURCE-BUILD-INSTRUCTIONS.md').write_bytes(instructions)
    for name, value in [('runtime-distribution-manifest.json', manifest), ('source-provision.json', inventory['sourceBundle'])]:
        (stage/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
    (stage/'README.md').write_text('# HIP Runtime 10.1.0\n\n'+platform['packageId']+'\n\n'
        'AMD HIP Runtime 7.16.26385 and HIPRTC 9.0, distributed from official AMD 10.1.0 artifacts. '
        'Use with JYPPX.ROCm.HIP.CSharp.API 1.0.0. Compatible AMD hardware and host driver are required. '
        'No SDK PATH is required. Known upstream native limitations remain documented in the Core support guide.\n\n'
        'Corresponding LGPL source archives, official build modifications and notices are provided at:\n\n'
        +inventory['sourceBundle']['url']+'\n\nSHA-256: '+inventory['sourceBundle']['sha256']+'\n\n'
        +'\n'.join('- '+p for p in platform['hostPrerequisites'])+'\n\n'
        'Third-party licenses apply to their respective components. No restriction is imposed on replacing '
        'LGPL shared libraries or reverse engineering to debug their modifications. '
        'Windows interop binary portions retain the included AMD EULA, including AMD hardware restrictions; '
        'third-party license precedence remains as stated by that EULA.\n',encoding='utf-8')
    with (stage/'THIRD-PARTY-NOTICES.txt').open('w',encoding='utf-8') as combined:
        combined.write('Individual components retain their original licenses. Inclusion of a notice for an optional '
                       'source-tree component does not relicense unrelated binaries. The source bundle preserves '
                       'all original texts and build material.\n\n')
        for name, data in sorted(notices.items()):
            combined.write('\n===== '+name+' =====\n'+data.decode('utf-8',errors='replace')+'\n')
    sbom = {'bomFormat':'CycloneDX','specVersion':'1.5','version':1,'components':[
        {'type':'file','name':f['name'],'hashes':[{'alg':'SHA-256','content':f['sha256']}]} for f in files]}
    (stage/'runtime.cdx.json').write_text(json.dumps(sbom,indent=2)+'\n',encoding='utf-8')
    content = [{'path':p.relative_to(stage).as_posix(),'sha256':digest(p)} for p in sorted(stage.rglob('*')) if p.is_file()]
    (stage/'distribution-files.json').write_text(json.dumps(content,indent=2)+'\n',encoding='utf-8')
    spec = ET.Element('package'); metadata = ET.SubElement(spec,'metadata')
    for key, value in dict(id=platform['packageId'],version=inventory['version'],authors='JYPPX',
                          description='Official AMD HIP and HIPRTC user-mode closure. Host AMD driver required.',
                          readme='README.md',requireLicenseAcceptance=str(platform['licenseAcceptance']).lower()).items():
        ET.SubElement(metadata,key).text=value
    ET.SubElement(metadata,'license',type='file').text='THIRD-PARTY-NOTICES.txt'
    ET.SubElement(metadata,'repository',type='git',url='https://github.com/guojin-yan/HIP-CSharp-API',commit=commit)
    payload = ET.SubElement(spec,'files')
    for path in sorted(stage.rglob('*')):
        if path.is_file():
            ET.SubElement(payload,'file',src=str(path),target=path.relative_to(stage).as_posix())
    ET.ElementTree(spec).write(output/'runtime.nuspec',encoding='utf-8',xml_declaration=True)
    (output/'runtime.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        '<TargetFramework>net10.0</TargetFramework><EnableDefaultItems>false</EnableDefaultItems>'
        '<IncludeBuildOutput>false</IncludeBuildOutput><NuspecFile>runtime.nuspec</NuspecFile>'
        '<NuGetAudit>false</NuGetAudit></PropertyGroup></Project>',encoding='utf-8')
    # net10.0 pack needs no third-party packages; an empty local feed prevents network restore.
    feed = output/'empty-feed'; feed.mkdir()
    subprocess.run(['dotnet','pack',str(output/'runtime.csproj'),'-c','Release','--output',str(output/'packages'),
                    '-p:RestoreSources='+str(feed)],check=True)
    package = output/'packages'/(platform['packageId']+'.'+inventory['version']+'.nupkg')
    verify(package, platform_name, inventory)
    result = dict(path=str(package),sha256=digest(package),bytes=package.stat().st_size,sourceCommit=commit,
                  platform=platform_name,scope='audited-runtime-distribution-candidate')
    (output/'package-audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=('pack','verify','verify-source'))
    parser.add_argument('--platform',choices=('win-x64','ubuntu.24.04-x64'))
    parser.add_argument('--binary-archive',type=Path)
    parser.add_argument('--source-bundle',type=Path)
    parser.add_argument('--output',type=Path)
    parser.add_argument('--package',type=Path)
    args = parser.parse_args()
    if args.command == 'pack':
        require(all((args.binary_archive,args.source_bundle,args.output,args.platform)), 'Missing pack inputs')
        pack(args.binary_archive,args.source_bundle,args.output,args.platform)
    elif args.command == 'verify-source':
        require(args.source_bundle is not None, 'Source bundle required')
        source_materials(args.source_bundle,lock()); print('Source material hashes verified.')
    else:
        require(args.package is not None and args.platform is not None, 'Package and platform required')
        verify(args.package,args.platform); print('Runtime distribution package verified.')


if __name__ == '__main__':
    main()
