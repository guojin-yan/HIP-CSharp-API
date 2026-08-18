# Managed expansion validation / 高层托管扩展验证

`samples/validation/HipManagedExpansionValidation` is the real-GPU integration workload for the managed API
families added in M8.2 through M8.6. It compiles a fixed HIPRTC module and executes pitched memory,
memory-pool, explicit-graph, occupancy/cooperative-launch, and module-global paths through public
managed APIs only.

`samples/validation/HipManagedExpansionValidation` 是 M8.2-M8.6 高层托管 API 家族的真实 GPU 集成工作负载。
它编译固定 HIPRTC module，只通过公开高层托管 API 执行 pitched memory、memory-pool、explicit graph、
occupancy/cooperative launch 与 module-global 路径。

## Run / 运行

Use the architecture reported by `rocminfo` and bind the result to the exact clean commit:

使用 `rocminfo` 报告的架构，并把结果绑定到精确 clean commit：

```bash
dotnet run --project samples/validation/HipManagedExpansionValidation/HipManagedExpansionValidation.csproj \
  -c Release --no-build -- \
  --arch gfx1100 \
  --expected-commit 0123456789abcdef0123456789abcdef01234567 \
  --environment official-host \
  --graph-launch-repeats 3
```

The package-only gate passes `--environment package-only`. The offline contract check loads no HIP
library and can run on any development machine:

package-only gate 使用 `--environment package-only`。离线契约检查不会加载 HIP library，可在任意开发机运行：

```powershell
pwsh -NoProfile -File ./eng/verify-managed-expansion.ps1 -Configuration Release
```

## Result contract / 结果契约

The last workload line is one schema-versioned JSON object. It reports five ordered stages, total and
per-stage CPU/GPU comparisons, iteration counts, capability classifications, a managed negative for
every stage, capability subtests, the first
failure stage/index, and `performanceClaim=false`. Any failed stage makes the process return nonzero.

工作负载最后一行是带schema版本的单个JSON对象。它按顺序报告五个阶段、总计和逐阶段CPU/GPU比较、
iteration次数、capability分类、每阶段managed negative、capability子项、首个失败阶段/index以及
`performanceClaim=false`。任一阶段
失败都会令进程返回非零。

The permitted skip surface is narrow:

允许skip的范围严格限定为：

- M8.3 may skip when a memory-pool operation returns `HipError.NotSupported`;
- M8.3仅在memory-pool操作返回`HipError.NotSupported`时skip；
- the optional M8.4 graph-memory-node subtest may skip on NotSupported, but the regular explicit DAG
  must still pass;
- optional M8.4 graph-memory-node子项可因NotSupported skip，但普通explicit DAG仍必须通过；
- cooperative launch may skip only when `HipDevice.SupportsCooperativeLaunch` is false.
- cooperative launch仅在`HipDevice.SupportsCooperativeLaunch`为false时skip。

An available capability whose call or data oracle fails is a failure, not a skip. Symbol or ABI
evidence does not count as Runtime/GPU data evidence.

已声明可用的capability若调用或数据oracle失败，属于failure而不是skip。Symbol或ABI证据不能替代
Runtime/GPU数据证据。

## Evidence boundary / 证据边界

`tools/radeon/cloud-test.sh` runs the workload on an official ROCm host after the 109-entry owner
symbol and schema-7 ABI gates. `tools/radeon/runtime-gate.sh` builds a sixth clean consumer from the
same source and runs it in Ubuntu Base + PRoot with `/opt/rocm` hidden. Both gates parse the JSON and
reject a stale commit, failed stage, missing managed negative, invalid order, or performance claim.

`tools/radeon/cloud-test.sh` 在109-entry owner symbol与schema-7 ABI门禁后，于official ROCm host运行该
工作负载。`tools/radeon/runtime-gate.sh` 从相同源码构建第六个clean consumer，并在隐藏`/opt/rocm`的
Ubuntu Base + PRoot中运行。两条gate都会解析JSON，并拒绝过期commit、failed stage、缺失managed
negative、顺序错误或performance claim。

These remote paths require a new Owner-authorized session. Local build/self-test evidence proves the
workload contract and compilation only; it is not a real symbol, Runtime, loader-map, or GPU result.

上述远端路径要求Owner为当次会话重新授权。本地build/self-test证据只证明工作负载契约和编译，不是
真实symbol、Runtime、loader-map或GPU结果。
