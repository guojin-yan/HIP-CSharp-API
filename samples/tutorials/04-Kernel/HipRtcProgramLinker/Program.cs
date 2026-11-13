using System;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Rtc;

internal static class Program
{
    private static int Main(string[] args)
    {
        const string source = "extern \"C\" __global__ void LinkedKernel() {}";
        string architecture = args.Length == 0 ? "gfx1100" : args[0];
        try
        {
            // HIPRTC linking queries the current device through HIP Runtime.
            // Resolve it through Core first when native assets come from NuGet.
            var runtime = new HipRuntime();
            runtime.Initialize();
            var rtc = new HipRtc();
            using HipRtcProgram program = rtc.CreateProgram(source, "linker-input.hip");
            byte[] bitcode = program.CompileToBitcode(new[] { "--offload-arch=" + architecture, "-fgpu-rdc" });
            using HipRtcLinker linker = rtc.CreateLinker();
            linker.AddData(HipRtcJitInputType.LlvmBitcode, bitcode, "linker-input.bc");
            byte[] codeObject = linker.Complete();
            Console.WriteLine("Linked code object bytes: " + codeObject.Length);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
