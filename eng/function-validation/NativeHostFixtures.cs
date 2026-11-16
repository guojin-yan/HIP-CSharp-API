using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetHelperPointer();
    private static IntPtr LoadHelperPointer(string name)
    {
        string path = Environment.GetEnvironmentVariable("HIPSHARP_TEST_HELPER") ?? throw new InvalidOperationException("Compiled native helper is required.");
        string expected = Environment.GetEnvironmentVariable("HIPSHARP_TEST_HELPER_SHA256") ?? throw new InvalidOperationException("Missing native helper hash.");
        string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        Require(actual == expected && Path.GetDirectoryName(Path.GetFullPath(path)) == Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), "Native helper path/hash mismatch.");
        IntPtr library = NativeLibrary.Load(path);
        // Keep compiler registration alive until this isolated process exits.
        IntPtr pointer = Marshal.GetDelegateForFunctionPointer<GetHelperPointer>(NativeLibrary.GetExport(library, name))();
        Require(pointer != 0, "Helper returned a null registered symbol.");
        Console.WriteLine("NATIVE_HELPER=" + System.Text.Json.JsonSerializer.Serialize(new { path, sha256 = actual, symbol = name }));
        return pointer;
    }

    [StructLayout(LayoutKind.Sequential)] private struct HostLaunch
    {
        public HipDim3 Grid, Block;
        public UIntPtr Shared;
        public IntPtr Stream, Attributes;
        public uint AttributeCount;
    }
    [StructLayout(LayoutKind.Sequential)] private struct FunctionAttributes
    {
        public int BinaryVersion, CacheMode;
        public UIntPtr ConstantSize, LocalSize;
        public int MaximumDynamicShared, MaximumThreads, Registers, Carveout, PtxVersion;
        public UIntPtr SharedSize;
    }

    private static void HostRuntime()
    {
        IntPtr kernel = LoadHelperPointer("ValidationKernel"), pointer = 0, stream = 0, start = 0, stop = 0;
        Check(Raw.Malloc((IntPtr)(&pointer), sizeof(int))); int output = 0;
        try
        {
            Check(Raw.StreamCreate((IntPtr)(&stream))); Check(Raw.Memset(pointer, 0, sizeof(int)));
            IntPtr argument = (IntPtr)(&pointer);
            Check(Raw.LaunchKernel(kernel, new HipDim3(1), new HipDim3(1), (IntPtr)(&argument), 0, stream));
            Check(Raw.StreamSynchronize(stream)); Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == 5, "Host registered launch.");
            HostLaunch launch = new() { Grid = new HipDim3(1), Block = new HipDim3(1), Stream = stream };
            Check(Raw.LaunchKernelExC((IntPtr)(&launch), kernel, (IntPtr)(&argument)));
            Check(Raw.StreamSynchronize(stream)); Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == 10, "Extended host launch.");
            Check(Raw.ConfigureCall(new HipDim3(1), new HipDim3(1), 0, stream));
            Check(Raw.SetupArgument((IntPtr)(&pointer), (UIntPtr)IntPtr.Size, 0)); Check(Raw.LaunchByPtr(kernel));
            Check(Raw.StreamSynchronize(stream)); Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == 15, "Legacy configured launch.");
            Check(Raw.EventCreate((IntPtr)(&start))); Check(Raw.EventCreate((IntPtr)(&stop)));
            Check(Raw.ExtLaunchKernel(kernel, new HipDim3(1), new HipDim3(1), (IntPtr)(&argument), 0, stream, start, stop, 0));
            Check(Raw.EventSynchronize(stop)); Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == 20, "Event-tracked extended launch.");
            IntPtr function = 0; Check(Raw.GetFuncBySymbol((IntPtr)(&function), kernel)); Require(function != 0, "Registered function query.");
            CheckText(Raw.KernelNameRef(function)); CheckText(Raw.KernelNameRefByPtr(kernel, stream));
            Check(Raw.ModuleLaunchKernel(function, 1, 1, 1, 1, 1, 1, 0, stream, (IntPtr)(&argument), 0));
            Check(Raw.StreamSynchronize(stream)); Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == 25, "Resolved module function launch.");
        }
        finally
        {
            if (stop != 0) Check(Raw.EventDestroy(stop)); if (start != 0) Check(Raw.EventDestroy(start));
            if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.Free(pointer));
        }
    }

    private static void HostAttributes()
    {
        IntPtr kernel = LoadHelperPointer("ValidationKernel");
        FunctionAttributes attributes = default; Check(Raw.FuncGetAttributes((IntPtr)(&attributes), kernel));
        Require(attributes.MaximumThreads > 0 && attributes.Registers >= 0, "Host function attributes.");
        Check(Raw.FuncSetAttribute(kernel, 8, 0));
        int blocks = 0, grid = 0, blockSize = 0;
        Check(Raw.OccupancyMaxActiveBlocksPerMultiprocessor((IntPtr)(&blocks), kernel, 64, 0)); Require(blocks > 0, "Host active occupancy.");
        Check(Raw.OccupancyMaxActiveBlocksPerMultiprocessorWithFlags((IntPtr)(&blocks), kernel, 64, 0, 0)); Require(blocks > 0, "Flagged host occupancy.");
        Check(Raw.OccupancyMaxPotentialBlockSize((IntPtr)(&grid), (IntPtr)(&blockSize), kernel, 0, 0));
        Require(grid > 0 && blockSize > 0 && blockSize <= attributes.MaximumThreads, "Host potential occupancy.");
        UIntPtr dynamicBytes = 0;
        if (Capability(Raw.OccupancyAvailableDynamicSMemPerBlock((IntPtr)(&dynamicBytes), kernel, 1, 64)))
            Require(dynamicBytes < 16 * 1024 * 1024, "Unreasonable available dynamic shared memory.");
    }

    private static void HostSymbol()
    {
        IntPtr symbol = LoadHelperPointer("ValidationSymbol"), device = 0, stream = 0; UIntPtr size = 0;
        Check(Raw.GetSymbolSize((IntPtr)(&size), symbol)); Require(size == sizeof(int), "Registered global size.");
        Check(Raw.GetSymbolAddress((IntPtr)(&device), symbol)); Require(device != 0, "Registered global address.");
        int input = 123, output = 0;
        Check(Raw.MemcpyFromSymbol((IntPtr)(&output), symbol, sizeof(int), 0, 2)); Require(output == 91, "Initial registered global.");
        Check(Raw.MemcpyToSymbol(symbol, (IntPtr)(&input), sizeof(int), 0, 1));
        Check(Raw.MemcpyDtoH((IntPtr)(&output), device, sizeof(int))); Require(output == input, "Global address/copy agreement.");
        Check(Raw.StreamCreate((IntPtr)(&stream)));
        try
        {
            input = 345; output = 0;
            Check(Raw.MemcpyToSymbolAsync(symbol, (IntPtr)(&input), sizeof(int), 0, 1, stream));
            Check(Raw.MemcpyFromSymbolAsync((IntPtr)(&output), symbol, sizeof(int), 0, 2, stream));
            Check(Raw.StreamSynchronize(stream)); Require(output == input, "Async registered global.");
            RegisteredSymbolGraph(symbol, device);
        }
        finally { Check(Raw.StreamDestroy(stream)); }
    }

    private static void RegisteredSymbolGraph(IntPtr symbol, IntPtr device)
    {
        IntPtr graph = 0, executable = 0, to = 0, from = 0, host = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0)); Check(Raw.HostMalloc((IntPtr)(&host), 16, 0));
        try
        {
            int* values = (int*)host; values[0] = 41; values[1] = 0; values[2] = 42; values[3] = 0;
            Check(Raw.GraphAddMemcpyNodeToSymbol((IntPtr)(&to), graph, 0, 0, symbol, host, sizeof(int), 0, 1));
            Check(Raw.GraphAddMemcpyNodeFromSymbol((IntPtr)(&from), graph, (IntPtr)(&to), 1, host + 4, symbol, sizeof(int), 0, 2));
            Check(Raw.GraphMemcpyNodeSetParamsToSymbol(to, symbol, host + 8, sizeof(int), 0, 1));
            Check(Raw.GraphMemcpyNodeSetParamsFromSymbol(from, host + 12, symbol, sizeof(int), 0, 2));
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); Require(values[3] == 42 && values[1] == 0, "Symbol graph parameter update.");
            Check(Raw.GraphExecMemcpyNodeSetParamsToSymbol(executable, to, symbol, host, sizeof(int), 0, 1));
            Check(Raw.GraphExecMemcpyNodeSetParamsFromSymbol(executable, from, host + 4, symbol, sizeof(int), 0, 2));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); Require(values[1] == 41, "Executable symbol graph update.");
            int query = 0; Check(Raw.MemcpyDtoH((IntPtr)(&query), device, sizeof(int))); Require(query == 41, "Graph global backing storage.");
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); Check(Raw.HostFree(host));
        }
    }
}
