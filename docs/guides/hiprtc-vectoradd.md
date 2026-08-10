# HIPRTC VectorAdd / HIPRTC 向量加法

`HipRtc` compiles HIP C++ source entirely in memory. A successful `HipRtcCompilation` owns a managed code-object snapshot, compiler log, compile-option snapshot, size, and SHA-256. The core API never selects a GPU architecture implicitly; pass the architecture reported by the target environment as a HIPRTC option.

`HipRtc` 完全在内存中编译 HIP C++ 源码。成功的 `HipRtcCompilation` 保存托管 code object 副本、编译日志、选项快照、大小与 SHA-256。核心 API 不会隐式猜测 GPU 架构；请把目标环境报告的架构作为 HIPRTC 选项传入。

```csharp
var rtc = new HipRtc();
using HipRtcProgram program = rtc.CreateProgram(source, "kernel.hip");
HipRtcCompilation output = program.Compile(new[] { "--offload-arch=gfx1100", "-O2" });

var runtime = new HipRuntime();
runtime.Initialize();
using HipModule module = runtime.LoadModule(output.GetCodeObject());
HipKernel kernel = module.GetKernel("VectorAdd");
kernel.Launch(
    new HipLaunchDimensions(gridSize),
    new HipLaunchDimensions(blockSize),
    new[]
    {
        HipKernelArgument.DevicePointer(deviceA),
        HipKernelArgument.DevicePointer(deviceB),
        HipKernelArgument.DevicePointer(deviceC),
        HipKernelArgument.Scalar32(length),
    });
runtime.Synchronize();
```

M2 launches only on the default stream. Each `kernelParams` slot points to stable native storage containing the argument value. A kernel retains its module owner, and launching after module disposal fails. Do not explicitly dispose device-memory arguments concurrently with `Launch`.

M2 只在 default stream 上启动。每个 `kernelParams` 槽位都指向保存参数值的稳定原生存储。Kernel 保持 module 所有者引用，module 释放后再次启动会失败。不要在 `Launch` 执行期间从其他线程显式释放参数中的设备内存。

Run the complete sample with the target architecture:

```bash
dotnet run --project samples/HipRtcVectorAdd/HipRtcVectorAdd.csproj -c Release -- --arch gfx1100 --length 1000 --repeat 20
```

Use `--negative-compile` to verify that an intentional syntax error produces `HipRtcException` with a non-empty `CompilationLog`.

The M2 gate passed this workflow on one authorized Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 / `gfx1100` instance for lengths `1`, `127`, `256`, `1000`, and `1048576`, each repeated 20 times. This is validation evidence for that environment, not a general support or performance claim.
