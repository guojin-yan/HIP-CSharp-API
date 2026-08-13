using System;
using System.Collections.Generic;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Rtc;

const string KernelSource = @"
extern ""C"" __global__ void VectorAdd(const float* a, const float* b, float* c, int length)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (index < length)
    {
        c[index] = a[index] + b[index];
    }
}";

try
{
    Options parsed = Options.Parse(args);
    var rtc = new HipRtc();
    HipRtcVersion rtcVersion = rtc.GetVersion();
    string[] compileOptions = { "--offload-arch=" + parsed.Architecture, "-O2" };

    if (parsed.NegativeCompile)
    {
        using HipRtcProgram invalidProgram = rtc.CreateProgram("extern \"C\" __global__ void Broken( {", "intentional-error.hip");
        try
        {
            invalidProgram.Compile(compileOptions);
            Console.Error.WriteLine("Expected HIPRTC compilation to fail, but it succeeded.");
            return 1;
        }
        catch (HipRtcException exception) when (exception.Result == HipRtcResult.Compilation && !string.IsNullOrWhiteSpace(exception.CompilationLog))
        {
            Console.WriteLine("Expected HIPRTC compile failure captured.");
            Console.WriteLine("HIPRTC result: " + exception.Result);
            Console.WriteLine("Compilation log present: true");
            Console.WriteLine(exception.CompilationLog);
            return 0;
        }
    }

    using HipRtcProgram program = rtc.CreateProgram(KernelSource, "vector-add.hip");
    HipRtcCompilation compilation = program.Compile(compileOptions);

    var runtime = new HipRuntime();
    runtime.Initialize();
    using HipModule module = runtime.LoadModule(compilation.GetCodeObject());
    HipKernel kernel = module.GetKernel("VectorAdd");

    float[] a = CreateInput(parsed.Length, 97, 1);
    float[] b = CreateInput(parsed.Length, 31, 2);
    var c = new float[parsed.Length];
    byte[] aBytes = ToBytes(a);
    byte[] bBytes = ToBytes(b);
    var cBytes = new byte[checked(parsed.Length * sizeof(float))];
    ulong byteLength = (ulong)cBytes.LongLength;

    using HipDeviceMemory deviceA = runtime.Allocate(byteLength);
    using HipDeviceMemory deviceB = runtime.Allocate(byteLength);
    using HipDeviceMemory deviceC = runtime.Allocate(byteLength);
    deviceA.CopyFrom(aBytes);
    deviceB.CopyFrom(bBytes);

    const uint blockSize = 256;
    uint gridSize = checked((uint)((parsed.Length + (long)blockSize - 1) / blockSize));
    var arguments = new List<HipKernelArgument>
    {
        HipKernelArgument.DevicePointer(deviceA),
        HipKernelArgument.DevicePointer(deviceB),
        HipKernelArgument.DevicePointer(deviceC),
        HipKernelArgument.Scalar32(parsed.Length),
    };

    for (int iteration = 0; iteration < parsed.Repeat; iteration++)
    {
        kernel.Launch(new HipLaunchDimensions(gridSize), new HipLaunchDimensions(blockSize), arguments);
        runtime.Synchronize();
        deviceC.CopyTo(cBytes);
        Buffer.BlockCopy(cBytes, 0, c, 0, cBytes.Length);
        int failureIndex = FindFailure(a, b, c);
        if (failureIndex >= 0)
        {
            Console.Error.WriteLine("VectorAdd mismatch at index " + failureIndex + ".");
            return 1;
        }
    }

    Console.WriteLine("HIPRTC version: " + rtcVersion);
    Console.WriteLine("Compile options: " + string.Join(" ", compilation.Options));
    Console.WriteLine("Compilation log present: " + (!string.IsNullOrWhiteSpace(compilation.Log)).ToString().ToLowerInvariant());
    Console.WriteLine("Code size: " + compilation.CodeSize);
    Console.WriteLine("Code SHA-256: " + compilation.CodeSha256);
    Console.WriteLine("VectorAdd passed: length=" + parsed.Length + "; repeat=" + parsed.Repeat + "; failureIndex=-1");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static float[] CreateInput(int length, int modulus, int multiplier)
{
    var values = new float[length];
    for (int index = 0; index < values.Length; index++)
    {
        values[index] = (index % modulus) * multiplier;
    }

    return values;
}

static byte[] ToBytes(float[] values)
{
    var bytes = new byte[checked(values.Length * sizeof(float))];
    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
    return bytes;
}

static int FindFailure(float[] a, float[] b, float[] actual)
{
    for (int index = 0; index < actual.Length; index++)
    {
        if (actual[index] != a[index] + b[index])
        {
            return index;
        }
    }

    return -1;
}

internal sealed class Options
{
    private Options(string architecture, int length, int repeat, bool negativeCompile)
    {
        Architecture = architecture;
        Length = length;
        Repeat = repeat;
        NegativeCompile = negativeCompile;
    }

    internal string Architecture { get; }

    internal int Length { get; }

    internal int Repeat { get; }

    internal bool NegativeCompile { get; }

    internal static Options Parse(string[] arguments)
    {
        string? architecture = Environment.GetEnvironmentVariable("HIPSHARP_GPU_ARCH");
        int length = 1000;
        int repeat = 20;
        bool negativeCompile = false;
        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--arch":
                    architecture = ReadValue(arguments, ref index, "--arch");
                    break;
                case "--length":
                    length = ParsePositive(ReadValue(arguments, ref index, "--length"), "--length");
                    break;
                case "--repeat":
                    repeat = ParsePositive(ReadValue(arguments, ref index, "--repeat"), "--repeat");
                    break;
                case "--negative-compile":
                    negativeCompile = true;
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arguments[index]);
            }
        }

        if (string.IsNullOrWhiteSpace(architecture))
        {
            throw new ArgumentException("Specify --arch gfxNNNN or set HIPSHARP_GPU_ARCH. The managed API does not guess a GPU architecture.");
        }

        return new Options(architecture, length, repeat, negativeCompile);
    }

    private static string ReadValue(string[] arguments, ref int index, string option)
    {
        index++;
        if (index >= arguments.Length)
        {
            throw new ArgumentException("Missing value for " + option + ".");
        }

        return arguments[index];
    }

    private static int ParsePositive(string value, string option)
    {
        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            throw new ArgumentException(option + " must be a positive 32-bit integer.");
        }

        return parsed;
    }
}
