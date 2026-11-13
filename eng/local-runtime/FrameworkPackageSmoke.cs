using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
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
            int observed = 0;
            using Process process = Process.GetCurrentProcess();
            foreach (ProcessModule loaded in process.Modules)
            {
                string path = Path.GetFullPath(loaded.FileName);
                if (!expected.TryGetValue(Path.GetFileName(path), out string hash)) continue;
                if (!path.StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Native library outside application: " + path);
                using SHA256 sha = SHA256.Create();
                using FileStream source = File.OpenRead(path);
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(source)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Native library hash mismatch: " + path);
                observed++;
                Console.WriteLine("PACKAGE_MODULE=" + path);
            }
            if (observed < 3) throw new InvalidOperationException("Expected packaged Runtime, HIPRTC and COMGR modules.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.GetType().FullName + ": " + exception.Message);
            return 1;
        }
    }
}
