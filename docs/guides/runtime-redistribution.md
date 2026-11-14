# Native redistribution status / 原生依赖再分发状态

The 10.1.0 local packages are **not public release candidates**. Fixed installed-file hashes prove local identity; they do not establish official distribution provenance or satisfy every redistribution obligation. The package lock is `eng/local-runtime-10.1.0.lock.json`; its download entries pin the archive/notice URL and SHA-256. The packer preserves downloaded source archives outside source and includes notices and inventory in the local nupkg.

## Reproducible archive mapping / 可重复的归档映射

`eng/local-runtime-provenance-10.1.0.lock.json` now maps every installed binary used by the packer to its exact member in the AMD `rocm_sdk_core` wheel. The Windows archive SHA-256 is `b12f1c4cde14ed1cd8864006d17586c6b1e6a4e4695b9597cfe04ed7715e952f`; the Linux archive is `a1a2433334ac61a526c6bae2f58f6bbc9b525f5a9e008e76a1a48d07f64f0703`. All 5 Windows / 18 Linux input binaries and the SDK build manifest match the wheel bytes. Linux's three NuGet aliases and `$ORIGIN` rewrite remain additional packaging transformations recorded by the package manifest.

Run the audit with the original downloaded wheel and installed SDK; all outputs go outside source:

```powershell
python eng/audit-local-runtime-provenance.py --platform win-x64 `
  --wheel <rocm_sdk_core-10.1.0-py3-none-win_amd64.whl> --hip-root <installed-HIP> `
  --downloads-cache <pack-local-runtime-downloads> --build-source <TheRock-source.tar.gz> `
  --output-root <new-external-directory> --online
```

Use `--platform ubuntu.24.04-x64` with the Linux wheel/SDK under Linux. Both audits preserve full source archives already pinned by the local packer and the TheRock build-source archive at commit `b324869fb3116353ac7eec5e6926aa3d452ac987` (SHA-256 `f8e68923c54fb2b42dadf0cd7778bd92246a3843ec50c601d6df396c19eb0caa`). Original download URLs and hashes are in the two locks. No source archive is extracted or executed by the audit.

`--online` checks the local wheel's size and multipart MD5 ETag against AMD's HTTPS endpoint, including its canonical `/rocm/core/whl-next/` redirect. This ties the local archive to current publisher metadata, while SHA-256 pins its exact local bytes. An S3 multipart ETag is **not** Authenticode, a detached publisher signature or a published SHA-256. Offline mode labels that publisher check as unperformed.

The output records binary/member hashes, submodule revisions, SDK build flags, source archive inventory and TheRock patch/recipe paths. In particular, `patches=[]` for a submodule in the SDK manifest does not imply no third-party changes: TheRock contains elfutils source/install modifications and a numactl configure patch. These are now preserved with the source materials. ROCDXG's build recipe identifies `rocm-systems/projects/rocr-runtime/libhsakmt` as its source and requires Windows SDK headers. Both audits retain `completeCorrespondingSource=false` and `publicReleaseValidated=false` until the remaining gates below are satisfied.

## Component mapping / 组件映射

| Payload family | Source/license material already pinned | Work required before public redistribution |
| --- | --- | --- |
| HIP Runtime, HIPRTC and builtins | rocm-systems HIP/CLR notices at commit `7da43026e4f2ef7600433c38c820732f7a205d22` | Match installed build to complete component source revisions, generated inputs and TheRock patches |
| COMGR, LLVM/Clang | SDK COMGR notice and LLVM notice at `d6f6cb691863e9258034ab48c1dfce21689d5606` | Account for statically linked dependencies and exact build configuration; a notice alone is not corresponding source |
| kpack | rocm-systems kpack notice at the above commit | Exact build/source mapping and embedded dependency inventory |
| HSA, rocprofiler-register | Installed SDK notices, fixed hashes | Exact binary-to-source/build mapping |
| rocdxg | Exact AMD wheel member/hash, rocm-systems revision and TheRock WSL recipe | Full source/notice and build mapping, Windows SDK prerequisites and WSL host boundary |
| bzip2 1.0.8, xz 5.8.1, zlib 1.3.2, zstd 1.5.7 | Hash-pinned upstream archives via TheRock third-party storage | Map private renamed libraries and any patches/build flags to exact corresponding sources |
| elfutils 0.195 (`librocm_sysdeps_elf.so.1`) | Hash-pinned source archive including license texts | Identify applicable LGPL/GPL files, modifications, corresponding source and any relinking/source-provision requirements |
| numactl 2.0.19 (`librocm_sysdeps_numa.so.1`) | Hash-pinned source archive | Complete LGPL compliance/source-provision mapping for the actual library build |
| libdrm 2.4.134 and AMDGPU support | Hash-pinned source archive, embedded notices | Preserve all applicable notices and map build changes |
| msgpack-cxx 7.0.0 and statically linked code | Hash-pinned archive | Verify complete embedded inventory; do not assume the dynamic dependency list is exhaustive |
| Host driver, DXCore, glibc, MSVC/UCRT | Declared prerequisites; excluded from Runtime payload | Document supported host versions and any installer terms separately |

The Linux staged ELF files have rewritten `$ORIGIN` RPATHs. An eventual source/build offer must describe those modifications. Keep sources and notices available through the required distribution period and delivery mechanism for the applicable licenses; an upstream URL by itself is not a completed source offer.

## Open release gates / 未完成门禁

Obtain verifiable upstream distribution provenance (including archive origin/hash or publisher signatures where applicable), record exact component versions/revisions and patches, complete the binary/source/license/build mapping, and review the required source provision for the actual distribution. Windows's five local DLLs currently report `NotSigned`; the historical Authenticode requirement remains in force for that release path. Neither this document nor the local hash policy waives it.

The Core release workflow publishes only the managed package. Its bundled Runtime dependencies do not acquire redistribution approval by passing GPU tests. The existing public Ubuntu Runtime 7.2.1 and its historical promotion receipt remain separate from these 10.1.0 materials.
