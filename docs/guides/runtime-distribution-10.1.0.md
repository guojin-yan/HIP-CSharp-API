# Runtime 10.1.0 distribution preparation

The new 10.1.0 distribution path uses pinned official AMD artifacts and a separate
corresponding-source delivery. It does not promote or rename the old local-only
packages, change the historical public 7.2.1 receipt, or maintain private AMD patches.
Packages remain candidates until their exact-package receipts and source delivery
have been checked and the publication workflow succeeds.

## Official binary inputs

`eng/runtime-distribution-10.1.0.lock.json` pins the complete input archive and every
source/output native file. Windows uses AMD's stable signed
`therock-dist-windows-gfx110X-all-10.1.0.tar.gz`, SHA-256
`1313633026b05f8f6af2a6a16a2f323211dd81a904a808f0ee34d2bbfb8f8441`.
All five shipped DLLs have valid Advanced Micro Devices Authenticode signatures.
The upstream archive contains `rocm_package_version=10.1.0rc3`, although its
published stable filename and `rocm_version` are 10.1.0. Its TheRock commit,
component commits and build ID match the original wheel. Excluding only PE
checksum/certificate metadata and terminal certificate padding, all five DLLs have
identical executable content to the original local 10.1.0 inputs. The actual signed
bytes are preserved and independently tested; no certificate is added by this project.

AMD's [signing discussion](https://github.com/ROCm/TheRock/issues/8538#issuecomment-5872445350)
explains why wheel files are unsigned while Windows tarballs are signed.
[The signing implementation discussion](https://github.com/ROCm/TheRock/issues/8788)
also identifies original build 36179538173. This resolves the unsigned-wheel
obstacle without weakening the existing AMD signer requirement.

Ubuntu uses the original fixed 10.1.0 AMD wheel. The staged ELF files and aliases
retain the same hashes as the previous `.3` local Runtime: only `$ORIGIN` RPATH and
three HIP alias copies are packaging changes. No rebuilt verification library is
substituted into the release payload.

## Source and license delivery

The locked source ZIP includes 17 complete archives: TheRock, rocm-systems,
LLVM/Clang/COMGR, SPIR-V translator, rocm-cmake, half, SIMDe, msgpack, fmt, glog,
bzip2, xz, zlib, zstd, libdrm, elfutils and numactl. Every archive and 267 extracted
notice files has an individual SHA-256 in the lock. The original licenses remain
inside those archives, including notices embedded in libdrm source headers.

The LGPL provision covers the actually shipped shared `librocm_sysdeps_elf.so.1`
and `librocm_sysdeps_numa.so.1`, with original source, TheRock modifications and
build/install recipes. Both have been rebuilt offline. Replacing those two shared
libraries in an isolated copy also passed HIPRTC compilation and GPU 41→42.
That verifies the replacement route; it does not claim AMD toolchain byte reproduction.
The package documentation allows replacement and debugging of modifications under
the applicable LGPL terms. Permissive components retain their notices; distributing
their full source is additional material, not a claim that every license requires it.

Windows PAL/interop material includes both MIT headers and distinct AMD binary
EULA terms. These are preserved, rather than treating the entire Windows closure
as MIT. Windows NuGet metadata requires license acceptance. AMD hardware/driver
restrictions remain applicable to those binary portions, with third-party license
precedence as stated in the original AMD EULA. No GPU driver, firmware, Windows SDK,
MSVC/UCRT installer, compiler executable or math library is bundled.

The source ZIP must be accessible at the lock's source-provision URL before any
Runtime upload. Preserve that source delivery for the applicable license periods;
an upstream URL alone is not the project's source delivery.

The [source delivery](https://github.com/guojin-yan/HIP-CSharp-API/releases/tag/runtime-sources-10.1.0)
is now public. Its 525,358,462-byte ZIP has SHA-256
`3459cd140f7172a57c1cc6bdb872f01e7750727a4da17b96fdb5cb9ce00960ae`;
an unauthenticated complete download was checked against that hash.

## Build and verify outside source

Use .NET SDK 10.0.300 and Python 3.11 or newer. There are no network downloads in
the packer. Supply the exact binary archive and prepared source ZIP:

```powershell
python -B eng/runtime-distribution.py pack --platform win-x64 `
  --binary-archive <official-signed-tarball> --source-bundle <locked-source.zip> `
  --output <new-directory-outside-source>
python -B eng/runtime-distribution.py verify --platform win-x64 --package <candidate.nupkg>
```

For Ubuntu 24.04, use `--platform ubuntu.24.04-x64`, the locked Linux wheel and
installed `patchelf`. The packer verifies the official archive/member hashes,
source/notice hashes, staged identities and native dependency closure. On Windows
it also checks x64 PE imports and valid AMD signatures. It writes a manifest,
CycloneDX file inventory, source-provision record, notices and short license paths.
It requires a clean checkout and creates all intermediate files outside source.

Run `eng/test-local-runtime-package.ps1 -DistributionRuntime` with the exact
candidate and Core package for deployment testing. The option validates the new
distribution format against its pinned inventory; the old local and historical
public package paths keep their original checks. Native module filename/path/hash
audits, missing-HIPRTC rejection, published deployment, and Windows net48/RID tests
still run. Native upstream defects and capability exclusions in the Core support
guide remain separate from packaging correctness.

## Publication control

`eng/runtime-distribution-release.py` binds both exact Runtime packages to clean
Windows and Linux deployment receipts, the already published Core 1.0.0 bytes,
and the fixed source provision. The reviewed `candidate.json` hash is the approval
anchor; these receipts are not remote GPU attestations. The publication workflow
downloads this reviewed bundle from a public staging prerelease, checks it before
requesting credentials, verifies the complete public source ZIP and AMD DLL
signatures, and uses NuGet Trusted Publishing with short-lived GitHub OIDC credentials.
The policy is restricted to `runtime-distribution-release.yml`, the protected
`runtime-production` environment, and the two exact Runtime package IDs.

Only reviewed package bytes are uploaded. Both public NuGet repository signatures
and every non-signature ZIP entry are checked after publication. A partially
completed publication can resume only if any existing public version matches the
reviewed payload exactly. The workflow does not rebuild packages or promote the
old local-only or 7.2.1 paths.
