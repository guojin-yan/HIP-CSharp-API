using System;
using System.Collections.Generic;
using System.Globalization;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Peer;
using JYPPX.ROCm.HipSharp.Rtc;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
private const string KernelSource = @"
extern ""C"" __global__ void VectorAdd(const float* a, const float* b, float* c, int length)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (index < length) c[index] = a[index] + b[index];
}";

private static int Main(string[] args)
{
    try
    {
        Options options = Options.Parse(args);
        var runtime = new HipRuntime();
        runtime.Initialize();
        IReadOnlyList<HipDevice> devices = runtime.GetDevices();
        if (devices.Count == 0)
        {
            throw new InvalidOperationException("No HIP device is available.");
        }
        devices[0].MakeCurrent();

    var rtc = new HipRtc();
    using HipRtcProgram program = rtc.CreateProgram(KernelSource, "advanced-features.hip");
    HipRtcCompilation compilation = program.Compile(new[] { "--offload-arch=" + options.Architecture, "-O2" });
    using HipModule module = runtime.LoadModule(compilation.GetCodeObject());
    HipKernel kernel = module.GetKernel("VectorAdd");

    int failureIndex = -1;
    foreach (int length in options.Lengths)
    {
        failureIndex = RunGraphVectorAdd(runtime, kernel, length, options.GraphLaunchRepeats);
        if (failureIndex >= 0)
        {
            break;
        }
        ValidateManagedMemory(runtime, length);
    }

    string peerResult = ProbePeer(runtime, devices);
    if (failureIndex >= 0)
    {
        throw new InvalidOperationException("CPU/GPU mismatch at index " + failureIndex + ".");
    }

    for (int repeat = 0; repeat < options.LifecycleRepeats; repeat++)
    {
        using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
        using HipAsyncDeviceMemory memory = runtime.AllocateAsync(4, stream);
        memory.CopyFromAsync(new byte[4]);
        stream.Synchronize();
    }

    string stressResult = options.StressRounds == 0
        ? "stress=not-requested"
        : RunMultiStreamStress(runtime, kernel, options);

    Console.WriteLine(
        "AdvancedReliabilityStress passed; device=" + devices[0].Name +
        "; architecture=" + options.Architecture +
        "; lengths=" + string.Join(",", options.Lengths) +
        "; graphLaunchRepeats=" + options.GraphLaunchRepeats.ToString(CultureInfo.InvariantCulture) +
        "; lifecycleRepeats=" + options.LifecycleRepeats.ToString(CultureInfo.InvariantCulture) +
        "; asyncAllocation=true; managedMemory=true; graphCapture=true; " + peerResult + "; failureIndex=-1");
    Console.WriteLine(stressResult);
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        return 1;
    }
}

private static int RunGraphVectorAdd(HipRuntime runtime, HipKernel kernel, int length, int graphLaunchRepeats)
{
    float[] a = new float[length];
    float[] b = new float[length];
    float[] cpu = new float[length];
    for (int index = 0; index < length; index++)
    {
        a[index] = (index % 97) * 0.5f;
        b[index] = (index % 31) * 2.0f;
        cpu[index] = a[index] + b[index];
    }

    byte[] aBytes = ToBytes(a);
    byte[] bBytes = ToBytes(b);
    byte[] resultBytes = new byte[aBytes.Length];
    using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
    using HipAsyncDeviceMemory deviceA = runtime.AllocateAsync((ulong)aBytes.Length, stream);
    using HipAsyncDeviceMemory deviceB = runtime.AllocateAsync((ulong)bBytes.Length, stream);
    using HipAsyncDeviceMemory deviceC = runtime.AllocateAsync((ulong)resultBytes.Length, stream);
    using HipGraph graph = runtime.CaptureGraph(stream, capturedStream =>
    {
        deviceA.CopyFromAsync(aBytes);
        deviceB.CopyFromAsync(bBytes);
        kernel.Launch(capturedStream,
            new HipLaunchDimensions(checked((uint)((length + 255) / 256))),
            new HipLaunchDimensions(256),
            new[]
            {
                HipKernelArgument.DevicePointer(deviceA),
                HipKernelArgument.DevicePointer(deviceB),
                HipKernelArgument.DevicePointer(deviceC),
                HipKernelArgument.Scalar32(length),
            });
        deviceC.CopyToAsync(resultBytes);
    });
    using HipGraphExec executable = graph.Instantiate();
    for (int repeat = 0; repeat < graphLaunchRepeats; repeat++)
    {
        executable.Launch(stream);
    }
    stream.Synchronize();

    var gpu = new float[length];
    Buffer.BlockCopy(resultBytes, 0, gpu, 0, resultBytes.Length);
    for (int index = 0; index < length; index++)
    {
        if (gpu[index] != cpu[index])
        {
            return index;
        }
    }
    return -1;
}

private static void ValidateManagedMemory(HipRuntime runtime, int length)
{
    byte[] expected = ToBytes(CreateSequence(length));
    byte[] actual = new byte[expected.Length];
    using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
    using HipManagedMemory memory = runtime.AllocateManaged((ulong)expected.Length);
    memory.CopyFromHost(expected);
    memory.Advise(HipMemoryAdvise.SetReadMostly, 0);
    memory.PrefetchAsync(0, stream);
    stream.Synchronize();
    memory.CopyToHost(actual);
    for (int index = 0; index < actual.Length; index++)
    {
        if (actual[index] != expected[index])
        {
            throw new InvalidOperationException("Managed-memory mismatch at byte " + index + ".");
        }
    }
}

private static string RunMultiStreamStress(HipRuntime runtime, HipKernel kernel, Options options)
{
    int byteLength = checked(options.StressLength * sizeof(float));
    var a = new float[options.StressLength];
    var b = new float[options.StressLength];
    for (int index = 0; index < options.StressLength; index++)
    {
        a[index] = (index % 193) * 0.25f;
        b[index] = (index % 67) * 1.5f;
    }

    byte[] aBytes = ToBytes(a);
    byte[] bBytes = ToBytes(b);
    for (int round = 0; round < options.StressRounds; round++)
    {
        var lanes = new List<StressLane>(options.StressStreams);
        try
        {
            for (int lane = 0; lane < options.StressStreams; lane++)
            {
                StressLane stressLane = StressLane.Create(runtime, byteLength, options.StressLength);
                lanes.Add(stressLane);
                stressLane.Queue(kernel, aBytes, bBytes);
            }

            foreach (StressLane lane in lanes)
            {
                lane.Synchronize();
            }
            for (int laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
            {
                int failureIndex = lanes[laneIndex].FindFailure(a, b);
                if (failureIndex >= 0)
                {
                    throw new InvalidOperationException(
                        "Multi-stream stress mismatch at round " + round.ToString(CultureInfo.InvariantCulture) +
                        ", lane " + laneIndex.ToString(CultureInfo.InvariantCulture) +
                        ", index " + failureIndex.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }
        finally
        {
            for (int lane = lanes.Count - 1; lane >= 0; lane--)
            {
                lanes[lane].Dispose();
            }
        }
    }

    long maximumDeviceBytes = checked((long)byteLength * 3L * options.StressStreams);
    return "stress=passed(rounds=" + options.StressRounds.ToString(CultureInfo.InvariantCulture) +
        ",streams=" + options.StressStreams.ToString(CultureInfo.InvariantCulture) +
        ",length=" + options.StressLength.ToString(CultureInfo.InvariantCulture) +
        ",maxInFlightDeviceBytes=" + maximumDeviceBytes.ToString(CultureInfo.InvariantCulture) +
        ",cpuGpuCompared=true,performanceClaim=false)";
}

private static string ProbePeer(HipRuntime runtime, IReadOnlyList<HipDevice> devices)
{
    if (devices.Count < 2)
    {
        return "peer=skipped(device-count<2)";
    }
    bool capable = runtime.CanAccessPeer(0, 1);
    if (!capable)
    {
        return "peer=skipped(capability=false)";
    }
    HipDeviceMemory? source = null;
    try
    {
        byte[] expected = { 17, 34, 51, 68 };
        devices[1].MakeCurrent();
        source = runtime.Allocate((ulong)expected.Length);
        source.CopyFrom(expected);

        devices[0].MakeCurrent();
        using HipDeviceMemory destination = runtime.Allocate((ulong)expected.Length);
        using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
        using HipPeerAccess access = runtime.EnablePeerAccess(0, 1);
        if (!access.IsEnabled)
        {
            throw new InvalidOperationException("Peer capability was true but access was not enabled.");
        }
        access.CopyAsync(destination, source, (ulong)expected.Length, stream);
        stream.Synchronize();
        byte[] actual = new byte[expected.Length];
        destination.CopyTo(actual);
        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new InvalidOperationException("Peer-copy mismatch at byte " + index + ".");
            }
        }
        return "peer=passed(1->0,alreadyEnabled=" + access.WasAlreadyEnabled.ToString(CultureInfo.InvariantCulture) + ")";
    }
    finally
    {
        if (source is not null)
        {
            devices[1].MakeCurrent();
            source.Dispose();
        }
        devices[0].MakeCurrent();
    }
}

private static float[] CreateSequence(int length)
{
    var values = new float[length];
    for (int index = 0; index < length; index++)
    {
        values[index] = index % 113;
    }
    return values;
}

private static byte[] ToBytes(float[] values)
{
    var bytes = new byte[checked(values.Length * sizeof(float))];
    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
    return bytes;
}
}

internal sealed class Options
{
    private static readonly int[] DefaultLengths = { 1, 127, 256, 1000, 1048576 };

    private Options(
        string architecture,
        int[] lengths,
        int graphLaunchRepeats,
        int lifecycleRepeats,
        int stressRounds,
        int stressStreams,
        int stressLength)
    {
        Architecture = architecture;
        Lengths = lengths;
        GraphLaunchRepeats = graphLaunchRepeats;
        LifecycleRepeats = lifecycleRepeats;
        StressRounds = stressRounds;
        StressStreams = stressStreams;
        StressLength = stressLength;
    }

    internal string Architecture { get; }
    internal int[] Lengths { get; }
    internal int GraphLaunchRepeats { get; }
    internal int LifecycleRepeats { get; }
    internal int StressRounds { get; }
    internal int StressStreams { get; }
    internal int StressLength { get; }

    internal static Options Parse(string[] args)
    {
        string? architecture = Environment.GetEnvironmentVariable("HIPSHARP_GPU_ARCH");
        int graphLaunchRepeats = 3;
        int lifecycleRepeats = 100;
        int stressRounds = 0;
        int stressStreams = 4;
        int stressLength = 4194304;
        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--arch" when index + 1 < args.Length: architecture = args[++index]; break;
                case "--graph-launch-repeats" when index + 1 < args.Length: graphLaunchRepeats = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                case "--lifecycle-repeats" when index + 1 < args.Length: lifecycleRepeats = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                case "--stress-rounds" when index + 1 < args.Length: stressRounds = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                case "--stress-streams" when index + 1 < args.Length: stressStreams = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                case "--stress-length" when index + 1 < args.Length: stressLength = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException("Unknown argument: " + args[index]);
            }
        }
        if (string.IsNullOrWhiteSpace(architecture))
        {
            throw new ArgumentException("Pass --arch <gfx-target> or set HIPSHARP_GPU_ARCH.");
        }
        if (graphLaunchRepeats < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Graph launch repeats must be at least 2.");
        }
        if (lifecycleRepeats < 100)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Lifecycle repeats must be at least 100.");
        }
        if (stressRounds < 0 || stressRounds > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Stress rounds must be between 0 and 100.");
        }
        if (stressStreams < 2 || stressStreams > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Stress streams must be between 2 and 8.");
        }
        if (stressLength < 1048576 || stressLength > 16777216)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Stress length must be between 1,048,576 and 16,777,216 floats.");
        }
        return new Options(architecture!, DefaultLengths, graphLaunchRepeats, lifecycleRepeats, stressRounds, stressStreams, stressLength);
    }
}

internal sealed class StressLane : IDisposable
{
    private bool _disposed;

    private StressLane(
        HipStream stream,
        HipAsyncDeviceMemory deviceA,
        HipAsyncDeviceMemory deviceB,
        HipAsyncDeviceMemory deviceC,
        byte[] result,
        int length)
    {
        Stream = stream;
        DeviceA = deviceA;
        DeviceB = deviceB;
        DeviceC = deviceC;
        Result = result;
        Length = length;
    }

    private HipStream Stream { get; }
    private HipAsyncDeviceMemory DeviceA { get; }
    private HipAsyncDeviceMemory DeviceB { get; }
    private HipAsyncDeviceMemory DeviceC { get; }
    private byte[] Result { get; }
    private int Length { get; }

    internal static StressLane Create(HipRuntime runtime, int byteLength, int length)
    {
        HipStream? stream = null;
        HipAsyncDeviceMemory? deviceA = null;
        HipAsyncDeviceMemory? deviceB = null;
        HipAsyncDeviceMemory? deviceC = null;
        try
        {
            stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
            deviceA = runtime.AllocateAsync((ulong)byteLength, stream);
            deviceB = runtime.AllocateAsync((ulong)byteLength, stream);
            deviceC = runtime.AllocateAsync((ulong)byteLength, stream);
            return new StressLane(stream, deviceA, deviceB, deviceC, new byte[byteLength], length);
        }
        catch
        {
            try { deviceC?.Dispose(); }
            finally
            {
                try { deviceB?.Dispose(); }
                finally
                {
                    try { deviceA?.Dispose(); }
                    finally { stream?.Dispose(); }
                }
            }
            throw;
        }
    }

    internal void Queue(HipKernel kernel, byte[] a, byte[] b)
    {
        DeviceA.CopyFromAsync(a);
        DeviceB.CopyFromAsync(b);
        kernel.Launch(Stream,
            new HipLaunchDimensions(checked((uint)((Length + 255) / 256))),
            new HipLaunchDimensions(256),
            new[]
            {
                HipKernelArgument.DevicePointer(DeviceA),
                HipKernelArgument.DevicePointer(DeviceB),
                HipKernelArgument.DevicePointer(DeviceC),
                HipKernelArgument.Scalar32(Length),
            });
        DeviceC.CopyToAsync(Result);
    }

    internal void Synchronize() => Stream.Synchronize();

    internal int FindFailure(float[] a, float[] b)
    {
        var gpu = new float[Length];
        Buffer.BlockCopy(Result, 0, gpu, 0, Result.Length);
        for (int index = 0; index < gpu.Length; index++)
        {
            if (gpu[index] != a[index] + b[index])
            {
                return index;
            }
        }
        return -1;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        try { DeviceC.Dispose(); }
        finally
        {
            try { DeviceB.Dispose(); }
            finally
            {
                try { DeviceA.Dispose(); }
                finally { Stream.Dispose(); }
            }
        }
        _disposed = true;
    }
}
