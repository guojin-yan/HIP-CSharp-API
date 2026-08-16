# HIPRTC Program and Linker ownership / HIPRTC Program 与 Linker 所有权

Core `0.9.3` adds managed access to HIPRTC name expressions, LLVM bitcode, and the HIPRTC linker. These APIs are additive to the existing `HipRtcProgram.Compile` code-object path.

Core `0.9.3` 新增 HIPRTC name expression、LLVM bitcode 与 HIPRTC linker 的托管入口。这些 API 是对现有 `HipRtcProgram.Compile` code-object 路径的增量扩展。

```csharp
var rtc = new HipRtc();
using HipRtcProgram program = rtc.CreateProgram(source, "templated-vector-add.hip");
program.AddNameExpression("VectorAdd<float>");

byte[] bitcode = program.CompileToBitcode(new[]
{
    "--offload-arch=gfx1100",
    "-fgpu-rdc",
});
string kernelName = program.GetLoweredName("VectorAdd<float>");

byte[] codeObject;
using (HipRtcLinker linker = rtc.CreateLinker())
{
    linker.AddData(HipRtcJitInputType.LlvmBitcode, bitcode, "vector-add.bc");
    codeObject = linker.Complete();
}

using HipModule module = runtime.LoadModule(codeObject);
HipKernel kernel = module.GetKernel(kernelName);
```

`AddNameExpression` must run before compilation, and `GetLoweredName` must run after successful compilation. The native lowered-name pointer belongs to the program; the managed method copies it immediately, so the returned `string` remains valid after `HipRtcProgram.Dispose`.

ROCm 7.2.1 returns `HIPRTC_ERROR_NAME_EXPRESSION_NOT_VALID` when a registered expression is queried before compilation. The exact-SHA workload records that result as the `lowered-name-before-compile` lifecycle negative; it does not treat the failed lookup as a successful lowered-name result.

`AddNameExpression` 必须在编译前调用，`GetLoweredName` 必须在成功编译后调用。原生 lowered-name 指针归 program 所有；托管方法立即复制，因此返回的 `string` 在 `HipRtcProgram.Dispose` 后仍然有效。

ROCm 7.2.1 在编译前查询已注册 expression 时返回 `HIPRTC_ERROR_NAME_EXPRESSION_NOT_VALID`。exact-SHA workload 将该结果记录为 `lowered-name-before-compile` 生命周期负测，不会把失败查询当作成功的 lowered-name 结果。

`CompileToBitcode` returns a managed byte-array copy. AMD's linker accepts LLVM bitcode (`100`), bundled bitcode (`101`), bundled-bitcode archives (`102`), and SPIR-V (`103`). The first managed batch intentionally uses zero JIT options; the complete low-level API remains available for advanced `void**` option contracts.

`CompileToBitcode` 返回托管 byte-array 副本。AMD linker 接受 LLVM bitcode（`100`）、bundled bitcode（`101`）、bundled-bitcode archive（`102`）和 SPIR-V（`103`）。首个托管批次有意固定使用零 JIT options；需要高级 `void**` option 契约时仍可使用完整低层 API。

`HipRtcLinker.AddData` copies its input to unmanaged memory and retains that copy until successful `Dispose`; callers may mutate or release their original array immediately. `Complete` copies the link-state-owned output into a new `byte[]`. A successful completion prevents further additions or a second completion, but the link state still requires disposal. If native destruction fails, explicit disposal throws and can be retried without discarding retained inputs.

`HipRtcLinker.AddData` 把输入复制到非托管内存，并保留到成功 `Dispose`；调用方可立即修改或释放原数组。`Complete` 把 link-state-owned 输出复制到新的 `byte[]`。成功完成后禁止继续添加或再次完成，但 link state 仍必须释放。若原生销毁失败，显式 disposal 会抛异常并允许重试，期间不会丢弃保留的输入。

## Exact-SHA cloud workload / Exact-SHA 云 workload

The 0.9.3 cloud workload is deterministic and must run from a clean detached checkout of the candidate SHA using the exact audited package hash. It performs both `AddData` and `AddFile` linker paths from the same `-fgpu-rdc` bitcode, loads each linked code object as a module, launches the lowered templated kernel, and compares every GPU result with the CPU reference. Evidence must record the candidate SHA, package SHA-256, GPU architecture, lowered name, bitcode/code-object sizes and hashes, comparison count, and disposal outcomes.

0.9.3 云 workload 必须从候选 SHA 的 clean detached checkout 运行，并使用精确审计包哈希。它从同一份 `-fgpu-rdc` bitcode 分别执行 `AddData` 与 `AddFile` linker 路径，把两个链接结果加载为 module，启动 lowered template kernel，并逐项比较 CPU/GPU 结果。证据必须记录候选 SHA、包 SHA-256、GPU 架构、lowered name、bitcode/code-object 大小与哈希、比较次数和 disposal 结果。

Required negatives are: invalid/empty managed inputs, lowered-name lookup before compilation, name-expression addition after compilation, missing linker file, addition and completion after successful completion, use after disposal, and exactly-once/double-dispose behavior. A symbol scan is not a functional pass. Until this workload runs against the new SHA, all nine promoted ledger entries remain `cloudFunctionCoverage=not-tested`, `publishable=false`, and `releaseAuthorized=false`.

必需负测包括：无效或空托管输入、编译前读取 lowered name、编译后添加 name expression、缺失 linker 文件、成功完成后继续添加或再次完成、释放后使用，以及 exactly-once/double-dispose 行为。symbol scan 不等于功能通过。在针对新 SHA 运行该 workload 前，9 个提升项继续保持 `cloudFunctionCoverage=not-tested`、`publishable=false` 和 `releaseAuthorized=false`。
