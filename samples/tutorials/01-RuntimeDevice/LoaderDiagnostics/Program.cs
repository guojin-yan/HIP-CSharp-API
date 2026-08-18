using System;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Loading;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            using var runtime = new HipRuntime();
            runtime.Initialize();
            Console.WriteLine("HIP Runtime loaded successfully.");
            return 0;
        }
        catch (HipLibraryLoadException exception)
        {
            Console.WriteLine(exception.Message);
            Console.WriteLine("Operating system: " + exception.Diagnostics.OperatingSystem);
            Console.WriteLine("Runtime identifier: " + exception.Diagnostics.RuntimeIdentifier);
            foreach (HipLibraryLoadAttempt attempt in exception.Diagnostics.Attempts)
            {
                Console.WriteLine((attempt.Succeeded ? "loaded: " : "failed: ") + attempt.Candidate + " (" + attempt.Detail + ")");
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
