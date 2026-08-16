using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
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

const string LinkerKernelSource = @"
template <typename T>
__global__ void VectorAddTemplate(const T* a, const T* b, T* c, int length)
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

    if (parsed.ProgramLinkerValidation)
    {
        return RunProgramLinkerValidation(rtc, parsed, LinkerKernelSource);
    }

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

static int RunProgramLinkerValidation(HipRtc rtc, Options options, string source)
{
    const string nameExpression = "VectorAddTemplate<float>";
    const string postCompilationNameExpression = "VectorAddTemplate<double>";
    var negatives = new List<string>();

    using (HipRtcProgram beforeCompile = rtc.CreateProgram(source, "lowered-before-compile.hip"))
    {
        beforeCompile.AddNameExpression(nameExpression);
        ExpectRtcResult(
            HipRtcResult.NameExpressionNotValid,
            () => beforeCompile.GetLoweredName(nameExpression),
            "lowered-name-before-compile",
            negatives);
    }

    byte[] bitcode;
    string loweredName;
    using (HipRtcProgram program = rtc.CreateProgram(source, "linked-vector-add.hip"))
    {
        program.AddNameExpression(nameExpression);
        bitcode = program.CompileToBitcode(new[]
        {
            "--offload-arch=" + options.Architecture,
            "-fgpu-rdc",
            "-O2",
        });
        loweredName = program.GetLoweredName(nameExpression);
        ExpectException<InvalidOperationException>(
            () => program.AddNameExpression(postCompilationNameExpression),
            "name-expression-after-compile",
            negatives);
    }

    byte[] dataCodeObject;
    var dataLinker = rtc.CreateLinker();
    byte[] mutableInput = (byte[])bitcode.Clone();
    dataLinker.AddData(HipRtcJitInputType.LlvmBitcode, mutableInput, "linked-vector-add.bc");
    Array.Clear(mutableInput, 0, mutableInput.Length);
    dataCodeObject = dataLinker.Complete();
    ExpectException<InvalidOperationException>(
        () => dataLinker.AddData(HipRtcJitInputType.LlvmBitcode, bitcode),
        "add-after-complete",
        negatives);
    ExpectException<InvalidOperationException>(() => dataLinker.Complete(), "complete-twice", negatives);
    dataLinker.Dispose();
    dataLinker.Dispose();
    ExpectException<ObjectDisposedException>(() => dataLinker.Complete(), "use-after-dispose", negatives);

    using (HipRtcLinker invalidInputLinker = rtc.CreateLinker())
    {
        ExpectException<ArgumentException>(
            () => invalidInputLinker.AddData(HipRtcJitInputType.LlvmBitcode, Array.Empty<byte>()),
            "empty-managed-input",
            negatives);
    }

    string missingPath = Path.Combine(Path.GetTempPath(), "hipsharp-missing-" + Guid.NewGuid().ToString("N") + ".bc");
    using (HipRtcLinker missingFileLinker = rtc.CreateLinker())
    {
        ExpectRtcResult(
            HipRtcResult.ProgramCreationFailure,
            () => missingFileLinker.AddFile(HipRtcJitInputType.LlvmBitcode, missingPath),
            "missing-linker-file",
            negatives);
    }

    byte[] fileCodeObject;
    string bitcodePath = Path.Combine(Path.GetTempPath(), "hipsharp-linker-" + Guid.NewGuid().ToString("N") + ".bc");
    try
    {
        File.WriteAllBytes(bitcodePath, bitcode);
        using HipRtcLinker fileLinker = rtc.CreateLinker();
        fileLinker.AddFile(HipRtcJitInputType.LlvmBitcode, bitcodePath);
        fileCodeObject = fileLinker.Complete();
    }
    finally
    {
        if (File.Exists(bitcodePath))
        {
            File.Delete(bitcodePath);
        }
    }

    var runtime = new HipRuntime();
    runtime.Initialize();
    long dataComparisons = RunLinkedCode(runtime, dataCodeObject, loweredName, options.Length, options.Repeat);
    long fileComparisons = RunLinkedCode(runtime, fileCodeObject, loweredName, options.Length, options.Repeat);

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        workload = "hiprtc-program-linker-0.9.3",
        status = "passed",
        repositoryCommit = options.ExpectedCommit,
        packageSha256 = options.ExpectedPackageSha256,
        environment = options.Environment,
        architecture = options.Architecture,
        loweredName,
        bitcodeSize = bitcode.Length,
        bitcodeSha256 = Sha256(bitcode),
        addDataCodeObjectSize = dataCodeObject.Length,
        addDataCodeObjectSha256 = Sha256(dataCodeObject),
        addFileCodeObjectSize = fileCodeObject.Length,
        addFileCodeObjectSha256 = Sha256(fileCodeObject),
        comparisons = checked(dataComparisons + fileComparisons),
        negatives,
        performanceClaim = false,
    }));
    return 0;
}

static long RunLinkedCode(HipRuntime runtime, byte[] codeObject, string kernelName, int length, int repeat)
{
    using HipModule module = runtime.LoadModule(codeObject);
    HipKernel kernel = module.GetKernel(kernelName);
    float[] a = CreateInput(length, 97, 1);
    float[] b = CreateInput(length, 31, 2);
    var actual = new float[length];
    byte[] output = new byte[checked(length * sizeof(float))];
    ulong byteLength = (ulong)output.LongLength;

    using HipDeviceMemory deviceA = runtime.Allocate(byteLength);
    using HipDeviceMemory deviceB = runtime.Allocate(byteLength);
    using HipDeviceMemory deviceC = runtime.Allocate(byteLength);
    deviceA.CopyFrom(ToBytes(a));
    deviceB.CopyFrom(ToBytes(b));

    const uint blockSize = 256;
    uint gridSize = checked((uint)((length + (long)blockSize - 1) / blockSize));
    var arguments = new List<HipKernelArgument>
    {
        HipKernelArgument.DevicePointer(deviceA),
        HipKernelArgument.DevicePointer(deviceB),
        HipKernelArgument.DevicePointer(deviceC),
        HipKernelArgument.Scalar32(length),
    };

    for (int iteration = 0; iteration < repeat; iteration++)
    {
        kernel.Launch(new HipLaunchDimensions(gridSize), new HipLaunchDimensions(blockSize), arguments);
        runtime.Synchronize();
        deviceC.CopyTo(output);
        Buffer.BlockCopy(output, 0, actual, 0, output.Length);
        int failureIndex = FindFailure(a, b, actual);
        if (failureIndex >= 0)
        {
            throw new InvalidOperationException("Linked VectorAdd mismatch at index " + failureIndex + ".");
        }
    }

    return checked((long)length * repeat);
}

static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

static void ExpectRtcResult(HipRtcResult expected, Action action, string name, List<string> negatives)
{
    try
    {
        action();
    }
    catch (HipRtcException exception) when (exception.Result == expected)
    {
        negatives.Add(name + "=passed(" + expected + ")");
        return;
    }

    throw new InvalidOperationException("Expected " + expected + " for negative case " + name + ".");
}

static void ExpectException<TException>(Action action, string name, List<string> negatives)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        negatives.Add(name + "=passed(" + typeof(TException).Name + ")");
        return;
    }

    throw new InvalidOperationException("Expected " + typeof(TException).Name + " for negative case " + name + ".");
}

internal sealed class Options
{
    private Options(
        string architecture,
        int length,
        int repeat,
        bool negativeCompile,
        bool programLinkerValidation,
        string expectedCommit,
        string expectedPackageSha256,
        string environment)
    {
        Architecture = architecture;
        Length = length;
        Repeat = repeat;
        NegativeCompile = negativeCompile;
        ProgramLinkerValidation = programLinkerValidation;
        ExpectedCommit = expectedCommit;
        ExpectedPackageSha256 = expectedPackageSha256;
        Environment = environment;
    }

    internal string Architecture { get; }

    internal int Length { get; }

    internal int Repeat { get; }

    internal bool NegativeCompile { get; }

    internal bool ProgramLinkerValidation { get; }

    internal string ExpectedCommit { get; }

    internal string ExpectedPackageSha256 { get; }

    internal string Environment { get; }

    internal static Options Parse(string[] arguments)
    {
        string? architecture = System.Environment.GetEnvironmentVariable("HIPSHARP_GPU_ARCH");
        int length = 1000;
        int repeat = 20;
        bool negativeCompile = false;
        bool programLinkerValidation = false;
        string expectedCommit = string.Empty;
        string expectedPackageSha256 = string.Empty;
        string environment = string.Empty;
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
                case "--program-linker-validation":
                    programLinkerValidation = true;
                    break;
                case "--expected-commit":
                    expectedCommit = ReadValue(arguments, ref index, "--expected-commit");
                    break;
                case "--expected-package-sha256":
                    expectedPackageSha256 = ReadValue(arguments, ref index, "--expected-package-sha256");
                    break;
                case "--environment":
                    environment = ReadValue(arguments, ref index, "--environment");
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arguments[index]);
            }
        }

        if (string.IsNullOrWhiteSpace(architecture))
        {
            throw new ArgumentException("Specify --arch gfxNNNN or set HIPSHARP_GPU_ARCH. The managed API does not guess a GPU architecture.");
        }

        if (programLinkerValidation)
        {
            if (!IsLowerHex(expectedCommit, 40))
            {
                throw new ArgumentException("--expected-commit must be a lowercase 40-character Git SHA.");
            }

            if (!IsLowerHex(expectedPackageSha256, 64))
            {
                throw new ArgumentException("--expected-package-sha256 must be a lowercase 64-character SHA-256.");
            }

            if (environment != "official-host" && environment != "package-only")
            {
                throw new ArgumentException("--environment must be official-host or package-only.");
            }
        }

        return new Options(
            architecture,
            length,
            repeat,
            negativeCompile,
            programLinkerValidation,
            expectedCommit,
            expectedPackageSha256,
            environment);
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

    private static bool IsLowerHex(string value, int length)
    {
        if (value.Length != length)
        {
            return false;
        }
        foreach (char character in value)
        {
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
