using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Rtc;
using JYPPX.ROCm.HipSharp.Streams;

internal static class FrameworkPackageSmoke
{
    private static unsafe int Main(string[] args)
    {
        try
        {
            // Exercise default discovery before any explicit Runtime load.
            var rtc = new HipRtc();
            Console.WriteLine("Framework HIPRTC: " + rtc.GetVersion());
            var raw = new HipRtcNativeApi();
            int major = 0, minor = 0;
            if (raw.Version((IntPtr)(&major), (IntPtr)(&minor)) != HipRtcResult.Success) return 1;
            using HipRtcProgram program = rtc.CreateProgram("extern \"C\" __global__ void Increment(int* value) { value[0]++; }", "framework.hip");
            HipRtcCompilation compiled = program.Compile(new[] { "--offload-arch=" + args[0] });
            using var runtime = new HipRuntime();
            runtime.Initialize();
            using HipModule module = runtime.LoadModule(compiled.GetCodeObject());
            using HipDeviceMemory memory = runtime.Allocate(4);
            memory.CopyFrom(BitConverter.GetBytes(41));
            using HipStream stream = runtime.CreateStream();
            module.GetKernel("Increment").Launch(stream, new HipLaunchDimensions(1), new HipLaunchDimensions(1), new[] { HipKernelArgument.DevicePointer(memory) });
            stream.Synchronize();
            byte[] result = new byte[4];
            memory.CopyTo(result);
            if (BitConverter.ToInt32(result, 0) != 42) return 1;
            Console.WriteLine("Framework GPU kernel: 42; raw HIPRTC: " + major + "." + minor);
            var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(args[1]))
            {
                string[] fields = line.Split('\t');
                expected.Add(fields[0], fields[1]);
            }
            using Process process = Process.GetCurrentProcess();
            var observed = PackageModuleAudit.Verify(AppDomain.CurrentDomain.BaseDirectory, expected,
                process.Modules.Cast<ProcessModule>().Select(loaded => loaded.FileName));
            foreach (string path in observed)
                Console.WriteLine("PACKAGE_MODULE=" + path);
            if (observed.Count < 3) throw new InvalidOperationException("Expected packaged Runtime, HIPRTC and COMGR modules.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.GetType().FullName + ": " + exception.Message);
            return 1;
        }
    }
}
