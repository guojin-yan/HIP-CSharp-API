using System;
using System.Threading;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Types;

// Linked into a PackageReference-only consumer; no SDK/native test doubles.
internal static class Program
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) when (error.GetType() == typeof(T)) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private static int Main(string[] args)
    {
        using var runtime = new HipRuntime();
        runtime.Initialize();
        using var stream = runtime.CreateStream();
        var advanced = runtime.AdvancedInterop;
        Require(!string.IsNullOrEmpty(advanced.GetDriverErrorName(HipError.InvalidValue)), "Driver error name is empty.");
        Require(!string.IsNullOrEmpty(advanced.GetDriverErrorString(HipError.InvalidValue)), "Driver error message is empty.");

        int callbackCount = 0;
        HipError callbackStatus = HipError.InvalidValue;
        advanced.AddStreamCallback(stream, status =>
        {
            callbackStatus = status;
            Interlocked.Increment(ref callbackCount);
            // Managed exceptions must be contained before returning to native HIP.
            throw new InvalidOperationException("Expected test callback exception.");
        });
        stream.Synchronize();
        stream.Synchronize();
        Require(callbackCount == 1 && callbackStatus == HipError.Success, "Native callback status/count mismatch.");
        Console.WriteLine("INTEROP_CALLBACK=passed; count=1; status=Success; userExceptionContained=true");

        Reject<ArgumentException>(() => advanced.ImportExternalMemory(default));
        Reject<ArgumentException>(() => advanced.ImportExternalSemaphore(default));
        Reject<ArgumentException>(() => advanced.CreateUserObject(IntPtr.Zero, default));
        Reject<ArgumentException>(() => advanced.CopyDriver2DUnaligned(default));
        Reject<ArgumentException>(() => advanced.CopyDriver3D(default));
        Reject<ArgumentException>(() => advanced.CopyDriver3DAsync(default, stream));
        Console.WriteLine("INTEROP_DEFAULT_DESCRIPTOR=passed; rejected=6; externalResourceRequired=false");

        runtime.Dispose();
        Reject<ObjectDisposedException>(() => advanced.AddStreamCallback(stream, _ => { }));
        Reject<ObjectDisposedException>(() => advanced.GetDriverErrorName(HipError.Success));
        Reject<ObjectDisposedException>(() => advanced.GetDriverErrorString(HipError.Success));
        Console.WriteLine("INTEROP_DISPOSED_FACADE=passed; rejected=3");
        return 0;
    }
}
