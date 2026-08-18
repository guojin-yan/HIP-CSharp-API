using System;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Rtc;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        const string source = "extern \"C\" __global__ void OccupancyKernel() {}";
        string architecture = args.Length == 0 ? "gfx1100" : args[0];
        try
        {
            var rtc = new HipRtc();
            using HipRtcProgram program = rtc.CreateProgram(source, "occupancy.hip");
            HipRtcCompilation compilation = program.Compile(new[] { "--offload-arch=" + architecture });
            using var runtime = new HipRuntime();
            runtime.Initialize();
            using HipModule module = runtime.LoadModule(compilation.GetCodeObject());
            HipKernel kernel = module.GetKernel("OccupancyKernel");
            HipOccupancyInfo occupancy = kernel.GetOccupancy(256);
            HipOccupancyPlan plan = kernel.GetOccupancyPlan();
            Console.WriteLine("Active blocks per multiprocessor: " + occupancy.MaximumResidentBlocks);
            Console.WriteLine("Suggested block size: " + plan.BlockSize + "; minimum grid: " + plan.MinimumGridSize);
            return 0;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: occupancy query is not supported by this HIP Runtime.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
