using System;
using System.Collections.Generic;
using System.Globalization;
using JYPPX.HipSharp;
using JYPPX.HipSharp.Graphs;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Peer;
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

    Console.WriteLine(
        "HipAdvancedFeatures passed; device=" + devices[0].Name +
        "; architecture=" + options.Architecture +
        "; lengths=" + string.Join(",", options.Lengths) +
        "; graphLaunchRepeats=" + options.GraphLaunchRepeats.ToString(CultureInfo.InvariantCulture) +
        "; lifecycleRepeats=" + options.LifecycleRepeats.ToString(CultureInfo.InvariantCulture) +
        "; asyncAllocation=true; managedMemory=true; graphCapture=true; " + peerResult + "; failureIndex=-1");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static int RunGraphVectorAdd(HipRuntime runtime, HipKernel kernel, int length, int graphLaunchRepeats)
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

static void ValidateManagedMemory(HipRuntime runtime, int length)
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

static string ProbePeer(HipRuntime runtime, IReadOnlyList<HipDevice> devices)
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
    devices[0].MakeCurrent();
    using HipPeerAccess access = runtime.EnablePeerAccess(0, 1);
    if (!access.IsEnabled)
    {
        throw new InvalidOperationException("Peer capability was true but access was not enabled.");
    }
    return "peer=enabled(0->1,alreadyEnabled=" + access.WasAlreadyEnabled.ToString(CultureInfo.InvariantCulture) + ")";
}

static float[] CreateSequence(int length)
{
    var values = new float[length];
    for (int index = 0; index < length; index++)
    {
        values[index] = index % 113;
    }
    return values;
}

static byte[] ToBytes(float[] values)
{
    var bytes = new byte[checked(values.Length * sizeof(float))];
    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
    return bytes;
}

internal sealed class Options
{
    private static readonly int[] DefaultLengths = { 1, 127, 256, 1000, 1048576 };

    private Options(string architecture, int[] lengths, int graphLaunchRepeats, int lifecycleRepeats)
    {
        Architecture = architecture;
        Lengths = lengths;
        GraphLaunchRepeats = graphLaunchRepeats;
        LifecycleRepeats = lifecycleRepeats;
    }

    internal string Architecture { get; }
    internal int[] Lengths { get; }
    internal int GraphLaunchRepeats { get; }
    internal int LifecycleRepeats { get; }

    internal static Options Parse(string[] args)
    {
        string? architecture = Environment.GetEnvironmentVariable("HIPSHARP_GPU_ARCH");
        int graphLaunchRepeats = 3;
        int lifecycleRepeats = 100;
        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--arch" when index + 1 < args.Length: architecture = args[++index]; break;
                case "--graph-launch-repeats" when index + 1 < args.Length: graphLaunchRepeats = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                case "--lifecycle-repeats" when index + 1 < args.Length: lifecycleRepeats = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
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
        return new Options(architecture!, DefaultLengths, graphLaunchRepeats, lifecycleRepeats);
    }
}
