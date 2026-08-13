using System;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;

try
{
    byte[] source = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
    var destination = new byte[source.Length];
    var runtime = new HipRuntime();
    runtime.Initialize();

    using (HipDeviceMemory first = runtime.Allocate((ulong)source.Length))
    using (HipDeviceMemory second = runtime.Allocate((ulong)source.Length))
    {
        first.CopyFrom(source);
        first.CopyTo(second, (ulong)source.Length);
        second.CopyTo(destination);
    }

    runtime.Synchronize();
    bool matched = source.SequenceEqual(destination);
    Console.WriteLine(matched ? "HIP H2D/D2D/D2H memory round trip passed." : "HIP memory round trip failed.");
    return matched ? 0 : 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
