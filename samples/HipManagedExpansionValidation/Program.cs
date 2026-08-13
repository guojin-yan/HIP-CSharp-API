using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Rtc;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private const int ElementCount = 64;
    private const int GlobalElementCount = 16;
    private const int TransformAddend = 7;
    private const int CooperativeAddend = 11;
    private const int FixedSeed = 8707;

    private const string KernelSource = @"
#include <hip/hip_runtime.h>
#include <hip/hip_cooperative_groups.h>

extern ""C"" {
__device__ int validation_values[16];
__device__ unsigned char validation_bytes[16];

__global__ void Transform(const int* input, int* output, int length)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (index < length) output[index] = input[index] + 7;
}

__global__ void ApplyGlobals(int* output, int length)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (index < length) output[index] = validation_values[index] + (int)validation_bytes[index];
}

__global__ void CooperativeTransform(const int* input, int* output, int length)
{
    cooperative_groups::grid_group grid = cooperative_groups::this_grid();
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (index < length) output[index] = input[index] + 11;
    grid.sync();
    if (index == 0 && length > 0) output[0] += 0;
}
}";

    private static int Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (Exception exception)
        {
            return WriteSetupFailure(string.Empty, "local", string.Empty, exception);
        }

        if (options.SelfTest || options.SelfTestFailure)
        {
            return RunSelfTest(options.SelfTestFailure);
        }

        var stages = new List<ValidationStageResult>();
        try
        {
            using var runtime = new HipRuntime();
            runtime.Initialize();
            IReadOnlyList<HipDevice> devices = runtime.GetDevices();
            if (devices.Count == 0)
            {
                throw new InvalidOperationException("No HIP device is available.");
            }

            HipDevice device = devices[0];
            device.MakeCurrent();

            var rtc = new HipRtc();
            using HipRtcProgram program = rtc.CreateProgram(KernelSource, "managed-expansion-validation.hip");
            HipRtcCompilation compilation = program.Compile(new[] { "--offload-arch=" + options.Architecture, "-O2" });
            byte[] codeObject = compilation.GetCodeObject();
            using HipModule module = runtime.LoadModule(codeObject);

            stages.Add(RunStage("m8.2-pitched-memory", () => ValidatePitchedMemory(runtime)));
            stages.Add(RunStage("m8.3-memory-pool", () => ValidateMemoryPool(runtime, device)));
            stages.Add(RunStage("m8.4-explicit-graph", () => ValidateExplicitGraphs(runtime, device, module, options.GraphLaunchRepeats)));
            stages.Add(RunStage("m8.5-kernel-occupancy", () => ValidateKernelCapabilities(runtime, device, module)));
            stages.Add(RunStage("m8.6-module-globals", () => ValidateModuleGlobals(runtime, module, codeObject)));

            ValidationSummary summary = ValidationSummary.Create(
                options.ExpectedCommit,
                options.Environment,
                options.Architecture,
                device.Name,
                stages);
            Console.WriteLine(summary.ToJson());
            return summary.Status == ValidationStatuses.Passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            if (stages.Count == 0)
            {
                return WriteSetupFailure(options.ExpectedCommit, options.Environment, options.Architecture, exception);
            }
            stages.Add(ValidationStageResult.Failed("setup", -1, Describe(exception)));
            ValidationSummary summary = ValidationSummary.Create(options.ExpectedCommit, options.Environment, options.Architecture, string.Empty, stages);
            Console.WriteLine(summary.ToJson());
            return 1;
        }
    }

    private static ValidationStageResult ValidatePitchedMemory(HipRuntime runtime)
    {
        long comparisons = 0;
        bool negative = false;
        using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
        using HipPitchedDeviceMemory<int> twoDimensional = runtime.Allocate2D<int>(7, 5);
        int[] source2D = Sequence(35, FixedSeed);
        int[] output2D = new int[source2D.Length];

        twoDimensional.SetZero();
        twoDimensional.CopyTo(output2D);
        comparisons += Compare(output2D, new int[output2D.Length]);
        twoDimensional.CopyFrom(source2D);
        Array.Clear(output2D, 0, output2D.Length);
        twoDimensional.CopyTo(output2D);
        comparisons += Compare(output2D, source2D);
        twoDimensional.SetZeroAsync(stream);
        twoDimensional.CopyFromAsync(source2D, stream);
        Array.Clear(output2D, 0, output2D.Length);
        twoDimensional.CopyToAsync(output2D, stream);
        stream.Synchronize();
        comparisons += Compare(output2D, source2D);

        try
        {
            twoDimensional.CopyFrom(new int[1]);
        }
        catch (ArgumentOutOfRangeException)
        {
            negative = true;
        }
        if (!negative)
        {
            throw new InvalidOperationException("The M8.2 undersized-array negative did not fail.");
        }

        using HipPitchedDeviceMemory<int> threeDimensional = runtime.Allocate3D<int>(5, 3, 2);
        threeDimensional.SetByte(0xA5);
        var region = new HipMemoryRegion(new HipMemoryOffset(1, 1, 0), new HipMemoryExtent(3, 2, 2));
        int[] regionSource = Sequence(12, FixedSeed + 1000);
        threeDimensional.CopyFrom(regionSource, region);
        int[] output3D = new int[30];
        threeDimensional.CopyTo(output3D);
        int sentinel = unchecked((int)0xA5A5A5A5u);
        for (int z = 0; z < 2; z++)
        {
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    int destinationIndex = x + (y * 5) + (z * 15);
                    int expected = sentinel;
                    if (x >= 1 && x < 4 && y >= 1)
                    {
                        int sourceIndex = (x - 1) + ((y - 1) * 3) + (z * 6);
                        expected = regionSource[sourceIndex];
                    }
                    comparisons += CompareValue(output3D[destinationIndex], expected, destinationIndex);
                }
            }
        }

        return ValidationStageResult.Passed("m8.2-pitched-memory", comparisons, negative);
    }

    private static ValidationStageResult ValidateMemoryPool(HipRuntime runtime, HipDevice device)
    {
        bool negative = false;
        try
        {
            runtime.CreateMemoryPool(null!);
        }
        catch (ArgumentNullException)
        {
            negative = true;
        }
        if (!negative)
        {
            throw new InvalidOperationException("The M8.3 null-options negative did not fail.");
        }

        try
        {
            var options = new HipMemoryPoolOptions(device)
            {
                ReleaseThresholdBytes = 4096,
                AllowEventDependencyReuse = true,
                AllowOpportunisticReuse = true,
                AllowInternalDependencyReuse = true,
            };
            using HipMemoryPool pool = runtime.CreateMemoryPool(options);
            if (!pool.OwnsHandle || pool.DeviceOrdinal != device.Ordinal)
            {
                throw new InvalidOperationException("Custom pool ownership is invalid.");
            }

            if (pool.ReleaseThresholdBytes != options.ReleaseThresholdBytes)
            {
                throw new InvalidOperationException("Pool release threshold did not round trip.");
            }

            if (!pool.AllowEventDependencyReuse || !pool.AllowOpportunisticReuse || !pool.AllowInternalDependencyReuse)
            {
                throw new InvalidOperationException("Pool reuse policy did not round trip.");
            }

            if (pool.GetAccess(device) != HipMemoryPoolAccess.ReadWrite)
            {
                throw new InvalidOperationException("The backing device lacks read-write pool access.");
            }

            using (HipMemoryPool defaultPool = runtime.GetDefaultMemoryPool(device))
            {
                if (defaultPool.OwnsHandle)
                {
                    throw new InvalidOperationException("Default pool view unexpectedly owns its handle.");
                }
            }
            using (HipMemoryPoolCurrentScope scope = pool.UseAsCurrent())
            using (HipMemoryPool currentPool = runtime.GetCurrentMemoryPool(device))
            {
                if (currentPool.OwnsHandle || currentPool.DeviceOrdinal != device.Ordinal)
                {
                    throw new InvalidOperationException("Current pool view ownership is invalid.");
                }
            }

            bool zeroByteNegative = false;
            using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
            try
            {
                pool.AllocateAsync(0, stream);
            }
            catch (ArgumentOutOfRangeException)
            {
                zeroByteNegative = true;
            }
            if (!zeroByteNegative)
            {
                throw new InvalidOperationException("The M8.3 zero-byte negative did not fail.");
            }

            byte[] source = SequenceBytes(256, FixedSeed + 2000);
            byte[] output = new byte[source.Length];
            HipPooledDeviceMemory memory = pool.AllocateAsync((ulong)source.Length, stream);
            memory.CopyFromAsync(source);
            memory.CopyToAsync(output);
            memory.Dispose();
            stream.Synchronize();
            long comparisons = Compare(output, source);
            HipMemoryPoolStatistics statistics = pool.GetStatistics();
            if (statistics.ReservedHighWatermarkBytes < statistics.ReservedBytes || statistics.UsedHighWatermarkBytes < statistics.UsedBytes)
            {
                throw new InvalidOperationException("Pool high-watermark statistics are inconsistent.");
            }

            pool.ResetReservedHighWatermark();
            pool.ResetUsedHighWatermark();
            pool.TrimTo(0);
            return ValidationStageResult.Passed("m8.3-memory-pool", comparisons, negative, capability: "available");
        }
        catch (HipException exception) when (IsMemoryPoolNotSupported(exception))
        {
            return ValidationStageResult.Skipped("m8.3-memory-pool", "not-supported:" + exception.Operation, negative);
        }
    }

    private static ValidationStageResult ValidateExplicitGraphs(HipRuntime runtime, HipDevice device, HipModule module, int repeats)
    {
        HipKernel transform = module.GetKernel("Transform");
        int[] input = Sequence(ElementCount, FixedSeed + 3000);
        int[] expected = input.Select(value => value + TransformAddend).ToArray();
        byte[] inputBytes = ToBytes(input);
        byte[] outputBytes = new byte[inputBytes.Length];
        long comparisons = 0;
        bool negative = false;
        var subtests = new List<ValidationSubtestResult>();

        using HipDeviceMemory source = runtime.Allocate((ulong)inputBytes.Length);
        using HipDeviceMemory working = runtime.Allocate((ulong)inputBytes.Length);
        source.CopyFrom(inputBytes);
        using HipGraph graph = runtime.CreateGraph();
        HipGraphNode clear = graph.AddMemset(working, 0, (ulong)inputBytes.Length);
        try
        {
            graph.AddDependency(clear, clear);
        }
        catch (ArgumentException)
        {
            negative = true;
        }
        if (!negative)
        {
            throw new InvalidOperationException("The M8.4 self-dependency negative did not fail.");
        }

        HipGraphNode copy = graph.AddCopy(source, working, (ulong)inputBytes.Length, new[] { clear });
        graph.AddKernel(
            transform,
            new HipLaunchDimensions(1),
            new HipLaunchDimensions((uint)ElementCount),
            new[] { HipKernelArgument.DevicePointer(working), HipKernelArgument.DevicePointer(working), HipKernelArgument.Scalar32(ElementCount) },
            new[] { copy });
        if (graph.Nodes.Count != 3 || graph.RootNodes.Count != 1 || graph.Edges.Count != 2)
        {
            throw new InvalidOperationException("Explicit graph topology is inconsistent.");
        }

        using HipGraphExec executable = graph.Instantiate();
        using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
        executable.Upload(stream);
        for (int repeat = 0; repeat < repeats; repeat++)
        {
            executable.Launch(stream);
            stream.Synchronize();
            working.CopyTo(outputBytes);
            comparisons += Compare(ToInts(outputBytes), expected);
        }
        subtests.Add(new ValidationSubtestResult("regular-explicit-dag", ValidationStatuses.Passed, "replays=" + repeats.ToString(CultureInfo.InvariantCulture)));

        try
        {
            using HipDeviceMemory graphOutput = runtime.Allocate((ulong)inputBytes.Length);
            using HipGraph memoryGraph = runtime.CreateGraph();
            HipGraphMemory local = memoryGraph.AddMemoryAllocation((ulong)inputBytes.Length, device);
            HipGraphNode localCopy = memoryGraph.AddCopy(source, local, (ulong)inputBytes.Length, new[] { local.AllocationNode });
            HipGraphNode localKernel = memoryGraph.AddKernel(
                transform,
                new HipLaunchDimensions(1),
                new HipLaunchDimensions((uint)ElementCount),
                new[] { HipKernelArgument.DevicePointer(local), HipKernelArgument.DevicePointer(graphOutput), HipKernelArgument.Scalar32(ElementCount) },
                new[] { localCopy });
            memoryGraph.AddMemoryFree(local, new[] { localKernel });
            using HipGraphExec memoryExecutable = memoryGraph.Instantiate();
            memoryExecutable.Upload(stream);
            memoryExecutable.Launch(stream);
            stream.Synchronize();
            graphOutput.CopyTo(outputBytes);
            comparisons += Compare(ToInts(outputBytes), expected);
            subtests.Add(new ValidationSubtestResult("graph-memory-nodes", ValidationStatuses.Passed, "alloc-copy-kernel-free"));
        }
        catch (HipException exception) when (IsGraphMemoryNodeNotSupported(exception))
        {
            subtests.Add(new ValidationSubtestResult("graph-memory-nodes", ValidationStatuses.Skipped, "not-supported:" + exception.Operation));
        }

        string graphMemoryCapability = subtests.Single(subtest => subtest.Name == "graph-memory-nodes").Status;
        return ValidationStageResult.Passed(
            "m8.4-explicit-graph",
            comparisons,
            negative,
            iterations: repeats,
            capability: "graph-memory=" + graphMemoryCapability,
            subtests: subtests);
    }

    private static ValidationStageResult ValidateKernelCapabilities(HipRuntime runtime, HipDevice device, HipModule module)
    {
        HipKernel kernel = module.GetKernel("CooperativeTransform");
        HipKernelAttributes attributes = kernel.GetAttributes();
        if (attributes.MaximumThreadsPerBlock <= 0)
        {
            throw new InvalidOperationException("Kernel attributes contain a non-positive maximum thread count.");
        }

        HipOccupancyPlan plan = kernel.GetOccupancyPlan(blockSizeLimit: 256);
        if (plan.BlockSize <= 0 ||
            plan.BlockSize > attributes.MaximumThreadsPerBlock ||
            plan.BlockSize > 256 ||
            plan.MinimumGridSize <= 0 ||
            plan.Occupancy.ActiveBlocksPerMultiprocessor <= 0)
        {
            throw new InvalidOperationException("Occupancy plan is outside the requested kernel bounds.");
        }

        if (plan.Occupancy.MultiprocessorCount != device.MultiprocessorCount || plan.Occupancy.MaximumResidentBlocks <= 0)
        {
            throw new InvalidOperationException("Occupancy plan is inconsistent with device facts.");
        }

        bool negative = false;
        try
        {
            kernel.GetOccupancy(0);
        }
        catch (ArgumentOutOfRangeException)
        {
            negative = true;
        }
        if (!negative)
        {
            throw new InvalidOperationException("The M8.5 zero-block negative did not fail.");
        }

        long comparisons = 0;
        var subtests = new List<ValidationSubtestResult>
        {
            new ValidationSubtestResult(
                "attributes-occupancy",
                ValidationStatuses.Passed,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "maxThreads={0};binaryVersion={1};block={2};minGrid={3};residentBlocks={4}",
                    attributes.MaximumThreadsPerBlock,
                    attributes.BinaryVersion,
                    plan.BlockSize,
                    plan.MinimumGridSize,
                    plan.Occupancy.MaximumResidentBlocks)),
        };
        bool cooperativeSupported = device.SupportsCooperativeLaunch;
        if (!cooperativeSupported)
        {
            subtests.Add(new ValidationSubtestResult("cooperative-launch", ValidationStatuses.Skipped, "capability=false"));
        }
        else
        {
            int[] input = Sequence(ElementCount, FixedSeed + 4000);
            int[] expected = input.Select(value => value + CooperativeAddend).ToArray();
            byte[] output = new byte[ElementCount * sizeof(int)];
            using HipDeviceMemory source = runtime.Allocate((ulong)output.Length);
            using HipDeviceMemory destination = runtime.Allocate((ulong)output.Length);
            using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
            source.CopyFrom(ToBytes(input));
            kernel.LaunchCooperative(
                stream,
                new HipLaunchDimensions(1),
                new HipLaunchDimensions((uint)ElementCount),
                new[] { HipKernelArgument.DevicePointer(source), HipKernelArgument.DevicePointer(destination), HipKernelArgument.Scalar32(ElementCount) });
            stream.Synchronize();
            destination.CopyTo(output);
            comparisons += Compare(ToInts(output), expected);
            subtests.Add(new ValidationSubtestResult("cooperative-launch", ValidationStatuses.Passed, "grid=1;block=" + ElementCount.ToString(CultureInfo.InvariantCulture)));
        }

        return ValidationStageResult.Passed(
            "m8.5-kernel-occupancy",
            comparisons,
            negative,
            capability: "cooperative=" + cooperativeSupported.ToString().ToLowerInvariant(),
            subtests: subtests);
    }

    private static ValidationStageResult ValidateModuleGlobals(HipRuntime runtime, HipModule module, byte[] codeObject)
    {
        HipModuleGlobal<int> values = module.GetGlobal<int>("validation_values");
        HipModuleGlobal bytes = module.GetGlobal("validation_bytes");
        if (values.ElementCount != GlobalElementCount || values.ByteLength != GlobalElementCount * sizeof(int) || bytes.ByteLength != GlobalElementCount)
        {
            throw new InvalidOperationException("Module-global extent is invalid.");
        }

        if (values.Name != "validation_values" || bytes.Name != "validation_bytes")
        {
            throw new InvalidOperationException("Module-global identity is invalid.");
        }

        int[] valueInput = Sequence(GlobalElementCount, FixedSeed + 5000);
        byte[] byteInput = SequenceBytes(GlobalElementCount, FixedSeed + 6000);
        int[] valueOutput = new int[GlobalElementCount];
        byte[] byteOutput = new byte[GlobalElementCount];
        long comparisons = 0;
        bool negative = false;
        using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);

        values.CopyFrom(valueInput);
        values.CopyTo(valueOutput);
        comparisons += Compare(valueOutput, valueInput);
        bytes.CopyFrom(byteInput);
        bytes.CopyTo(byteOutput);
        comparisons += Compare(byteOutput, byteInput);

        Array.Clear(valueOutput, 0, valueOutput.Length);
        Array.Clear(byteOutput, 0, byteOutput.Length);
        values.CopyFrom(new int[GlobalElementCount]);
        bytes.CopyFrom(new byte[GlobalElementCount]);
        values.CopyFromAsync(valueInput, stream);
        values.CopyToAsync(valueOutput, stream);
        bytes.CopyFromAsync(byteInput, stream);
        bytes.CopyToAsync(byteOutput, stream);
        stream.Synchronize();
        comparisons += Compare(valueOutput, valueInput);
        comparisons += Compare(byteOutput, byteInput);

        byte[] valueBytes = ToBytes(valueInput);
        using HipPinnedMemory pinned = runtime.AllocatePinned((ulong)valueBytes.Length);
        pinned.CopyFrom(valueBytes);
        values.CopyFrom(new int[GlobalElementCount]);
        values.CopyFrom(pinned, GlobalElementCount);
        values.CopyTo(valueOutput);
        comparisons += Compare(valueOutput, valueInput);

        pinned.CopyFrom(new byte[valueBytes.Length]);
        values.CopyTo(pinned, GlobalElementCount);
        byte[] pinnedOutput = new byte[valueBytes.Length];
        pinned.CopyTo(pinnedOutput);
        comparisons += Compare(pinnedOutput, valueBytes);

        values.CopyFrom(new int[GlobalElementCount]);
        values.CopyFromAsync(pinned, stream, GlobalElementCount);
        stream.Synchronize();
        values.CopyTo(valueOutput);
        comparisons += Compare(valueOutput, valueInput);

        pinned.CopyFrom(new byte[valueBytes.Length]);
        values.CopyToAsync(pinned, stream, GlobalElementCount);
        stream.Synchronize();
        pinned.CopyTo(pinnedOutput);
        comparisons += Compare(pinnedOutput, valueBytes);

        using HipDeviceMemory deviceMemory = runtime.Allocate((ulong)valueBytes.Length);
        deviceMemory.CopyFrom(valueBytes);
        values.CopyFrom(new int[GlobalElementCount]);
        values.CopyFrom(deviceMemory, GlobalElementCount);
        values.CopyTo(valueOutput);
        comparisons += Compare(valueOutput, valueInput);

        deviceMemory.CopyFrom(new byte[valueBytes.Length]);
        values.CopyTo(deviceMemory, GlobalElementCount);
        byte[] deviceOutput = new byte[valueBytes.Length];
        deviceMemory.CopyTo(deviceOutput);
        comparisons += Compare(deviceOutput, valueBytes);

        values.CopyFrom(new int[GlobalElementCount]);
        values.CopyFromAsync(deviceMemory, stream, GlobalElementCount);
        stream.Synchronize();
        values.CopyTo(valueOutput);
        comparisons += Compare(valueOutput, valueInput);

        deviceMemory.CopyFrom(new byte[valueBytes.Length]);
        values.CopyToAsync(deviceMemory, stream, GlobalElementCount);
        stream.Synchronize();
        deviceMemory.CopyTo(deviceOutput);
        comparisons += Compare(deviceOutput, valueBytes);

        values.CopyFrom(valueInput);
        bytes.CopyFrom(byteInput);
        using HipDeviceMemory kernelOutput = runtime.Allocate((ulong)valueBytes.Length);
        HipKernel applyGlobals = module.GetKernel("ApplyGlobals");
        applyGlobals.Launch(
            stream,
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(GlobalElementCount),
            new[] { HipKernelArgument.DevicePointer(kernelOutput), HipKernelArgument.Scalar32(GlobalElementCount) });
        stream.Synchronize();
        kernelOutput.CopyTo(deviceOutput);
        int[] expectedKernel = Enumerable.Range(0, GlobalElementCount).Select(index => valueInput[index] + byteInput[index]).ToArray();
        comparisons += Compare(ToInts(deviceOutput), expectedKernel);

        try
        {
            values.CopyTo(new int[GlobalElementCount + 1]);
        }
        catch (ArgumentOutOfRangeException)
        {
            negative = true;
        }
        if (!negative)
        {
            throw new InvalidOperationException("The M8.6 oversized-array negative did not fail.");
        }

        int[] pendingOutput = new int[GlobalElementCount];
        using HipModule pendingModule = runtime.LoadModule(codeObject);
        HipModuleGlobal<int> pendingValues = pendingModule.GetGlobal<int>("validation_values");
        pendingValues.CopyFrom(valueInput);
        pendingValues.CopyToAsync(pendingOutput, stream);
        pendingModule.Dispose();
        if (!pendingModule.IsDisposed)
        {
            throw new InvalidOperationException("Pending module did not enter disposed state.");
        }

        stream.Synchronize();
        comparisons += Compare(pendingOutput, valueInput);

        return ValidationStageResult.Passed("m8.6-module-globals", comparisons, negative);
    }

    private static ValidationStageResult RunStage(string name, Func<ValidationStageResult> action)
    {
        try
        {
            return action();
        }
        catch (ValidationFailureException exception)
        {
            return ValidationStageResult.Failed(name, exception.Index, "cpu-gpu-mismatch");
        }
        catch (Exception exception)
        {
            return ValidationStageResult.Failed(name, -1, Describe(exception));
        }
    }

    private static bool IsMemoryPoolNotSupported(HipException exception)
    {
        if (exception.Error != HipError.NotSupported)
        {
            return false;
        }

        return exception.Operation.StartsWith("hipMemPool", StringComparison.Ordinal) ||
            exception.Operation == "hipMallocFromPoolAsync" ||
            exception.Operation == "hipDeviceGetDefaultMemPool" ||
            exception.Operation == "hipDeviceGetMemPool" ||
            exception.Operation == "hipDeviceSetMemPool";
    }

    private static bool IsGraphMemoryNodeNotSupported(HipException exception) =>
        exception.Error == HipError.NotSupported &&
        (exception.Operation == "hipGraphAddMemAllocNode" || exception.Operation == "hipGraphAddMemFreeNode");

    private static int RunSelfTest(bool emitFailure)
    {
        var subtests = new[] { new ValidationSubtestResult("cooperative-launch", ValidationStatuses.Skipped, "capability=false") };
        ValidationStageResult[] stages =
        {
            ValidationStageResult.Passed("m8.2-pitched-memory", 1, true),
            ValidationStageResult.Skipped("m8.3-memory-pool", "not-supported:self-test", true),
            ValidationStageResult.Passed("m8.4-explicit-graph", 1, true),
            ValidationStageResult.Passed("m8.5-kernel-occupancy", 1, true, subtests: subtests),
            ValidationStageResult.Passed("m8.6-module-globals", 1, true),
        };
        ValidationSummary passed = ValidationSummary.Create(new string('0', 40), "self-test", "none", "none", stages);
        if (passed.Status != ValidationStatuses.Passed || passed.PerformanceClaim || passed.Comparisons != 4 || passed.SkippedStages.Count != 1)
        {
            throw new InvalidOperationException("Passed summary aggregation failed.");
        }

        ValidationSummary failed = ValidationSummary.Create(
            new string('0', 40),
            "self-test",
            "none",
            "none",
            new[] { stages[0], ValidationStageResult.Failed("m8.4-explicit-graph", 7, "self-test-failure") });
        if (failed.Status != ValidationStatuses.Failed || failed.FailureStage != "m8.4-explicit-graph" || failed.FailureIndex != 7)
        {
            throw new InvalidOperationException("Failure summary aggregation failed.");
        }

        Console.WriteLine((emitFailure ? failed : passed).ToJson());
        return emitFailure ? 1 : 0;
    }

    private static int WriteSetupFailure(string commit, string environment, string architecture, Exception exception)
    {
        ValidationSummary summary = ValidationSummary.Create(
            commit,
            environment,
            architecture,
            string.Empty,
            new[] { ValidationStageResult.Failed("setup", -1, Describe(exception)) });
        Console.WriteLine(summary.ToJson());
        return 1;
    }

    private static string Describe(Exception exception)
    {
        if (exception is HipException hip)
        {
            return hip.GetType().Name + ":" + hip.Operation + ":" + hip.Error;
        }

        if (exception is HipRtcException rtc)
        {
            return rtc.GetType().Name + ":" + rtc.Operation + ":" + rtc.Result;
        }

        return exception.GetType().Name;
    }

    private static int[] Sequence(int length, int seed) => Enumerable.Range(0, length).Select(index => seed + (index * 17)).ToArray();

    private static byte[] SequenceBytes(int length, int seed)
    {
        var result = new byte[length];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = (byte)((seed + (index * 13)) & 0xFF);
        }

        return result;
    }

    private static byte[] ToBytes(int[] values)
    {
        var result = new byte[checked(values.Length * sizeof(int))];
        Buffer.BlockCopy(values, 0, result, 0, result.Length);
        return result;
    }

    private static int[] ToInts(byte[] values)
    {
        if (values.Length % sizeof(int) != 0)
        {
            throw new ArgumentException("The byte array is not an Int32 sequence.", nameof(values));
        }

        var result = new int[values.Length / sizeof(int)];
        Buffer.BlockCopy(values, 0, result, 0, values.Length);
        return result;
    }

    private static long Compare(int[] actual, int[] expected)
    {
        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException("Comparison lengths differ.");
        }

        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new ValidationFailureException(index);
            }
        }

        return actual.LongLength;
    }

    private static long Compare(byte[] actual, byte[] expected)
    {
        if (actual.Length != expected.Length)
        {
            throw new InvalidOperationException("Comparison lengths differ.");
        }

        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                throw new ValidationFailureException(index);
            }
        }

        return actual.LongLength;
    }

    private static int CompareValue(int actual, int expected, int index)
    {
        if (actual != expected)
        {
            throw new ValidationFailureException(index);
        }

        return 1;
    }

    private sealed class ValidationFailureException : Exception
    {
        internal ValidationFailureException(int index) => Index = index;

        internal int Index { get; }
    }

    private sealed class Options
    {
        private static readonly Regex ArchitecturePattern = new("^gfx[0-9a-z]+$", RegexOptions.CultureInvariant);
        private static readonly Regex CommitPattern = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);

        private Options() { }

        internal bool SelfTest { get; private set; }

        internal bool SelfTestFailure { get; private set; }

        internal string Architecture { get; private set; } = string.Empty;

        internal string ExpectedCommit { get; private set; } = string.Empty;

        internal string Environment { get; private set; } = "official-host";

        internal int GraphLaunchRepeats { get; private set; } = 3;

        internal static Options Parse(string[] args)
        {
            var result = new Options();
            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--self-test":
                        result.SelfTest = true;
                        break;
                    case "--self-test-failure":
                        result.SelfTestFailure = true;
                        break;
                    case "--arch":
                        result.Architecture = Next(args, ref index, "--arch");
                        break;
                    case "--expected-commit":
                        result.ExpectedCommit = Next(args, ref index, "--expected-commit");
                        break;
                    case "--environment":
                        result.Environment = Next(args, ref index, "--environment");
                        break;
                    case "--graph-launch-repeats":
                        result.GraphLaunchRepeats = int.Parse(Next(args, ref index, "--graph-launch-repeats"), CultureInfo.InvariantCulture);
                        break;
                    default:
                        throw new ArgumentException("Unknown option: " + args[index]);
                }
            }

            if (result.SelfTest || result.SelfTestFailure)
            {
                if (result.SelfTest && result.SelfTestFailure)
                {
                    throw new ArgumentException("Only one self-test mode may be selected.");
                }
                return result;
            }

            if (!ArchitecturePattern.IsMatch(result.Architecture))
            {
                throw new ArgumentException("--arch must be a gfx architecture.");
            }

            if (!CommitPattern.IsMatch(result.ExpectedCommit))
            {
                throw new ArgumentException("--expected-commit must be a lowercase 40-character SHA.");
            }

            if (result.Environment != "official-host" && result.Environment != "package-only")
            {
                throw new ArgumentException("--environment must be official-host or package-only.");
            }

            if (result.GraphLaunchRepeats < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "Graph launch repeats must be at least three.");
            }

            return result;
        }

        private static string Next(string[] args, ref int index, string option)
        {
            index++;
            if (index >= args.Length)
            {
                throw new ArgumentException("Missing value for " + option + ".");
            }

            return args[index];
        }
    }
}
