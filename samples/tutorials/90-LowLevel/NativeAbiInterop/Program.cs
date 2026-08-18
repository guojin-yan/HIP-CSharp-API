using System;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var native = new HipRuntimeNativeApi();
            EnsureSuccess(native.Init(0), "hipInit");

            unsafe
            {
                int runtimeVersion = 0;
                int deviceCount = 0;
                EnsureSuccess(native.RuntimeGetVersion((IntPtr)(&runtimeVersion)), "hipRuntimeGetVersion");
                EnsureSuccess(native.GetDeviceCount((IntPtr)(&deviceCount)), "hipGetDeviceCount");
                Console.WriteLine("Raw Runtime version: " + runtimeVersion);
                Console.WriteLine("Raw device count: " + deviceCount);
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void EnsureSuccess(HipError error, string operation)
    {
        if (error != HipError.Success)
        {
            throw new InvalidOperationException(operation + " returned " + error + ".");
        }
    }
}
