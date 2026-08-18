using System;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Rtc;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        const string source = "extern \"C\" __device__ int values[4] = { 0, 0, 0, 0 };";
        string architecture = args.Length == 0 ? "gfx1100" : args[0];
        try
        {
            var rtc = new HipRtc();
            using HipRtcProgram program = rtc.CreateProgram(source, "module-globals.hip");
            HipRtcCompilation compilation = program.Compile(new[] { "--offload-arch=" + architecture });
            using var runtime = new HipRuntime();
            runtime.Initialize();
            using HipModule module = runtime.LoadModule(compilation.GetCodeObject());
            HipModuleGlobal<int> global = module.GetGlobal<int>("values");
            int[] expected = { 3, 5, 7, 11 };
            int[] actual = new int[expected.Length];
            global.CopyFrom(expected);
            global.CopyTo(actual);
            bool passed = expected.SequenceEqual(actual);
            Console.WriteLine(passed ? "Module global round trip passed." : "Module global round trip failed.");
            return passed ? 0 : 1;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: module globals are not supported by this HIP Runtime.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
