using System;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            using var runtime = new HipRuntime();
            runtime.Initialize();

            int[] twoDimensionalSource = Enumerable.Range(0, 12).ToArray();
            int[] twoDimensionalDestination = new int[twoDimensionalSource.Length];
            using (HipPitchedDeviceMemory<int> memory2D = runtime.Allocate2D<int>(4, 3))
            {
                memory2D.CopyFrom(twoDimensionalSource);
                memory2D.CopyTo(twoDimensionalDestination);
                Console.WriteLine($"2D extent={memory2D.Width}x{memory2D.Height}, pitch={memory2D.PitchBytes} bytes");
            }

            int[] threeDimensionalSource = Enumerable.Range(0, 24).ToArray();
            int[] threeDimensionalDestination = new int[threeDimensionalSource.Length];
            using (HipPitchedDeviceMemory<int> memory3D = runtime.Allocate3D<int>(4, 3, 2))
            {
                memory3D.CopyFrom(threeDimensionalSource);
                memory3D.CopyTo(threeDimensionalDestination);
                Console.WriteLine($"3D extent={memory3D.Width}x{memory3D.Height}x{memory3D.Depth}, pitch={memory3D.PitchBytes} bytes");
            }

            bool passed = twoDimensionalSource.SequenceEqual(twoDimensionalDestination) &&
                          threeDimensionalSource.SequenceEqual(threeDimensionalDestination);
            Console.WriteLine(passed ? "Pitched 2D/3D memory round trip passed." : "Pitched 2D/3D memory round trip failed.");
            return passed ? 0 : 1;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: pitched 2D/3D memory is not supported by this HIP Runtime.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
