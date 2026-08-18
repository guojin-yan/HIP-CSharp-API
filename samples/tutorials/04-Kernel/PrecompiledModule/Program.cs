using System;
using System.IO;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Modules;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || !File.Exists(args[0]))
            {
                Console.WriteLine("Usage: dotnet run --project ... -- <path-to-code-object> [kernel-name]");
                return 0;
            }
            using var runtime = new HipRuntime();
            runtime.Initialize();
            using HipModule module = runtime.LoadModule(File.ReadAllBytes(args[0]));
            HipKernel kernel = module.GetKernel(args.Length > 1 ? args[1] : "VectorAdd");
            Console.WriteLine("Loaded module and resolved kernel: " + kernel.Name);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
