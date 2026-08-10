using System;
using System.Collections.Generic;
using System.Globalization;
using JYPPX.HipSharp;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Rtc;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

const string KernelSource = @"
extern ""C"" __global__ void VectorAdd(const float* a, const float* b, float* c, int length)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (index < length) c[index] = a[index] + b[index];
}";

try
{
    Options options = Options.Parse(args);
    var rtc = new HipRtc();
    using HipRtcProgram program = rtc.CreateProgram(KernelSource, "stream-event-vector-add.hip");
    HipRtcCompilation compilation = program.Compile(new[] { "--offload-arch=" + options.Architecture, "-O2" });

    var runtime = new HipRuntime();
    runtime.Initialize();
    Console.WriteLine("Device clock (kHz): " + runtime.GetDeviceAttribute(HipDeviceAttribute.ClockRate));
    using HipModule module = runtime.LoadModule(compilation.GetCodeObject());
    HipKernel kernel = module.GetKernel("VectorAdd");
    using HipStream streamA = runtime.CreateStream(HipStreamFlags.NonBlocking);
    using HipStream streamB = runtime.CreateStream(HipStreamFlags.NonBlocking);
    using HipEvent start = runtime.CreateEvent();
    using HipEvent end = runtime.CreateEvent();

    bool expectedError = false;
    try
    {
        _ = runtime.GetDeviceAttribute(HipDeviceAttribute.ClockRate, -2);
    }
    catch (ArgumentOutOfRangeException)
    {
        expectedError = true;
        Console.WriteLine("Expected invalid-device-ordinal error captured.");
    }

    foreach (int length in options.Lengths)
    {
        RunVectorAdd(runtime, kernel, streamA, start, end, length, 0);
        RunVectorAdd(runtime, kernel, streamB, start, end, length, 1);
    }

    for (int index = 0; index < options.LifecycleRepeats; index++)
    {
        using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
        using HipEvent ev = runtime.CreateEvent(HipEventFlags.DisableTiming);
        ev.Record(stream);
        stream.Synchronize();
    }

    streamA.Dispose();
    streamA.Dispose();
    Console.WriteLine("stream/event VectorAdd passed; lengths=" + string.Join(",", options.Lengths) + "; lifecycleRepeats=" + options.LifecycleRepeats + "; repeatedDispose=true; expectedError=" + expectedError + "; failureIndex=-1");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static void RunVectorAdd(HipRuntime runtime, HipKernel kernel, HipStream stream, HipEvent start, HipEvent end, int length, int lane)
{
    float[] a = new float[length];
    float[] b = new float[length];
    float[] result = new float[length];
    for (int index = 0; index < length; index++)
    {
        a[index] = index % 97;
        b[index] = (index % 31) * 2;
    }

    byte[] aBytes = ToBytes(a);
    byte[] bBytes = ToBytes(b);
    byte[] resultBytes = new byte[aBytes.Length];
    using HipDeviceMemory deviceA = runtime.Allocate((ulong)aBytes.Length);
    using HipDeviceMemory deviceB = runtime.Allocate((ulong)bBytes.Length);
    using HipDeviceMemory deviceC = runtime.Allocate((ulong)resultBytes.Length);
    deviceA.CopyFromAsync(aBytes, stream);
    deviceB.CopyFromAsync(bBytes, stream);
    start.Record(stream);
    kernel.Launch(stream, new HipLaunchDimensions(checked((uint)((length + 255) / 256))), new HipLaunchDimensions(256), new[]
    {
        HipKernelArgument.DevicePointer(deviceA),
        HipKernelArgument.DevicePointer(deviceB),
        HipKernelArgument.DevicePointer(deviceC),
        HipKernelArgument.Scalar32(length),
    });
    deviceC.CopyToAsync(resultBytes, stream);
    end.Record(stream);
    stream.Synchronize();
    if (!end.Query())
    {
        throw new InvalidOperationException("Event should be complete after stream synchronization.");
    }
    _ = HipEvent.ElapsedTime(start, end);
    Buffer.BlockCopy(resultBytes, 0, result, 0, resultBytes.Length);
    for (int index = 0; index < result.Length; index++)
    {
        if (result[index] != a[index] + b[index])
        {
            throw new InvalidOperationException("VectorAdd mismatch at index " + index + " on lane " + lane + ".");
        }
    }
}

static byte[] ToBytes(float[] values)
{
    var bytes = new byte[checked(values.Length * sizeof(float))];
    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
    return bytes;
}

internal sealed class Options
{
    private Options(string architecture, int[] lengths, int lifecycleRepeats)
    {
        Architecture = architecture;
        Lengths = lengths;
        LifecycleRepeats = lifecycleRepeats;
    }

    internal string Architecture { get; }
    internal int[] Lengths { get; }
    internal int LifecycleRepeats { get; }

    internal static Options Parse(string[] args)
    {
        string? architecture = Environment.GetEnvironmentVariable("HIPSHARP_GPU_ARCH");
        if (string.IsNullOrWhiteSpace(architecture))
        {
            throw new ArgumentException("Pass --arch <gfx-target> or set HIPSHARP_GPU_ARCH.");
        }
        var lengths = new[] { 1, 127, 256, 1000, 1048576 };
        int repeats = 100;
        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--arch" when index + 1 < args.Length: architecture = args[++index]; break;
                case "--lifecycle-repeats" when index + 1 < args.Length: repeats = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException("Unknown argument: " + args[index]);
            }
        }
        if (repeats < 100)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Lifecycle repeats must be at least 100.");
        }
        return new Options(architecture!, lengths, repeats);
    }
}
