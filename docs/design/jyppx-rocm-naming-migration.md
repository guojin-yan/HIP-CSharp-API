# JYPPX ROCm naming migration / JYPPX ROCm 命名迁移

## Decision / 决策

HIP-CSharp-API remains an independent repository and product under the JYPPX ROCm product family. `JYPPX.ROCm` is a naming layer for namespaces, assemblies, and packages; it is not a public base assembly or NuGet package.

HIP-CSharp-API 继续作为 JYPPX ROCm 产品家族中的独立仓库和独立产品。`JYPPX.ROCm` 仅是命名空间、程序集和包名的家族层，不对应公共基础程序集或 NuGet 包。

| Asset / 资产 | Before / 迁移前 | Frozen name / 冻结名称 |
| --- | --- | --- |
| Root namespace / 根命名空间 | `JYPPX.HipSharp` | `JYPPX.ROCm.HipSharp` |
| Core assembly / 核心程序集 | `JYPPX.HipSharp.dll` | `JYPPX.ROCm.HIP.CSharp.API.dll` |
| Managed NuGet | `JYPPX.HIP.CSharp.API` | `JYPPX.ROCm.HIP.CSharp.API` |
| Linux runtime NuGet | `JYPPX.HipSharp.Runtime.linux-x64` | `JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64` |
| Windows runtime NuGet | `JYPPX.HipSharp.Runtime.win-x64` | `JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64` |

The repository name, GitHub URL, `HipSharp.sln`, public HIP namespaces and type/member names, native entry points, logical library names, and pinned HIP 7.2.1 headers do not change. The assembly identity is deliberately aligned with the managed NuGet package ID; it is independent of the `JYPPX.ROCm.HipSharp` root namespace.

仓库名、GitHub 地址、`HipSharp.sln`、公开 HIP 命名空间及类型/成员名、原生 EntryPoint、logical library 和固定 HIP 7.2.1 头文件均不改变。程序集 identity 明确与 managed NuGet 包 ID 对齐，并与 `JYPPX.ROCm.HipSharp` 根命名空间相互独立。

## Family and dependency boundary / 家族与依赖边界

MIGraphX remains in `MIGraphX-CSharp-API` as `JYPPX.ROCm.MIGraphX`. rocFFT, rocBLAS, and other algorithm libraries share the `ROCm-Libraries-CSharp-API` repository and solution, while each library retains its own project, assembly, NuGet, runtime package, and validation matrix. Strongly typed adapters are optional and module-specific, for example `JYPPX.ROCm.RocFft.HipSharp` and `JYPPX.ROCm.MIGraphX.HipSharp`.

MIGraphX 继续位于独立的 `MIGraphX-CSharp-API` 仓库并使用 `JYPPX.ROCm.MIGraphX`。rocFFT、rocBLAS 等算法库共享 `ROCm-Libraries-CSharp-API` 仓库和解决方案，但每个库保留独立项目、程序集、NuGet、runtime 包和验证矩阵。强类型 adapter 是可选且按模块建立的，例如 `JYPPX.ROCm.RocFft.HipSharp` 与 `JYPPX.ROCm.MIGraphX.HipSharp`。

The HipSharp Core never depends on an adapter, MIGraphX, or an algorithm library. No `JYPPX.ROCm`, `JYPPX.ROCm.Native`, `JYPPX.ROCm.Common`, or `JYPPX.ROCm.Runtime` project/package is created.

HipSharp Core 永远不依赖 adapter、MIGraphX 或算法库；不创建 `JYPPX.ROCm`、`JYPPX.ROCm.Native`、`JYPPX.ROCm.Common` 或 `JYPPX.ROCm.Runtime` 项目/包。

## Compatibility policy / 兼容策略

NuGet.org had no exact match for the former managed or runtime package IDs when this migration was performed. During the first publication attempt, Core `0.9.0` and Runtime `7.2.1` were published under identities that did not align with the final package family. The immutable versions are handled through a corrected Core `0.9.1` and a new Runtime package ID; there is no compatibility namespace, facade, forwarding assembly, legacy NuGet, or dual public surface.

执行迁移时，NuGet.org 对原 managed/runtime 包 ID 均无精确命中。首次发布时，Core `0.9.0` 与 Runtime `7.2.1` 的 identity 尚未与最终包族对齐。不可变版本通过修正后的 Core `0.9.1` 和新的 Runtime 包 ID 处理；不保留兼容命名空间、facade、类型转发程序集、旧 NuGet 或新旧双公开面。

The former public API snapshot is invalidated by this pre-release namespace decision. `eng/public-api/JYPPX.ROCm.HipSharp.0.9.0.txt`, generated from the renamed assembly, is the new freeze baseline. All ownership, disposal, error, ABI, and target-framework contracts remain unchanged.

原 public API snapshot 因发布前 namespace 决策作废。由新程序集生成的 `eng/public-api/JYPPX.ROCm.HipSharp.0.9.0.txt` 是新的冻结基线；所有 ownership、dispose、error、ABI 和目标框架契约保持不变。

## Affected assets and gates / 影响面与门禁

The migration covers project paths and references, explicit namespaces and usings, generator templates and outputs, public API categories/snapshot, DocFX, samples, tests, package consumers, package validation suppressions, runtime manifests/schema/provenance/SBOM, and build/package/supply-chain scripts.

Required gates are deterministic interop generation, 459 Runtime plus 18 HIPRTC low-level declarations, the current 109-entry managed-owner manifest, all 15 target frameworks, public API parity, managed package content and clean consumers, runtime manifest/supply-chain static tests, Windows skeleton rejection tests, DocFX, and old-name residue scans. Historical or static evidence is never upgraded to current GPU execution evidence.

Changing the Linux runtime package ID created a new package identity even though its native allowlist and source hashes were unchanged. The renamed base manifest therefore remained `packEnabled=false` and `verified=false` until a hash-bound clean-SHA candidate passed newly Owner-authorized exact-package validation in M8.7. M8.8 enables final packaging only through the deterministic M8.7 receipt and protected-payload equivalence; newly built final bytes still require a separate authorized final-mode gate.

Linux runtime 包 ID 的改变会创建新的包身份，即使 native allowlist 和来源哈希保持不变。重命名后的基础 manifest 因此曾保持 `packEnabled=false`、`verified=false`，直到绑定 clean SHA 的候选在 M8.7 通过新一次 Owner 授权的 exact-package 验证。M8.8 只通过确定性 M8.7 receipt 与受保护 payload 等价证明启用 final 打包；新生成的 final 字节仍须单独授权的 final-mode 门禁。

## Rollback boundary / 回滚边界

Rollback is an all-or-nothing source-control operation performed before publication. It must restore project paths, namespaces, assembly/package IDs, generator templates and outputs, public API assets, manifests, tests, and documentation together. Partial rollback or shipping both name families is forbidden. Native payloads, hashes, licenses, ABI declarations, and historical GPU evidence are outside this naming rollback and must not be regenerated or reinterpreted.
