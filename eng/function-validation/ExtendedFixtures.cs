using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    private static void Library()
    {
        byte[] image = Compile(false);
        for (int mode = 0; mode < 2; mode++)
        {
            IntPtr library = 0;
            if (mode == 0)
            {
                fixed (byte* data = image)
                    if (!Capability(Raw.LibraryLoadData((IntPtr)(&library), (IntPtr)data, 0, 0, 0, 0, 0, 0))) return;
            }
            else
            {
                string path = Path.Combine(Environment.GetEnvironmentVariable("HIPSHARP_TEST_WORK")!, "library.co"); File.WriteAllBytes(path, image);
                using var name = new Utf8(path); Check(Raw.LibraryLoadFromFile((IntPtr)(&library), name.Pointer, 0, 0, 0, 0, 0, 0));
            }
            Require(library != 0, "Library load returned null.");
            try
            {
                uint count = 0; Check(Raw.LibraryGetKernelCount((IntPtr)(&count), library)); Require(count == 1, "Library kernel count.");
                IntPtr kernel = 0; using var name = new Utf8("Increment");
                Check(Raw.LibraryGetKernel((IntPtr)(&kernel), library, name.Pointer)); Require(kernel != 0, "Library kernel is null.");
                IntPtr enumerated = 0; Check(Raw.LibraryEnumerateKernels((IntPtr)(&enumerated), 1, library)); Require(enumerated == kernel, "Kernel enumeration.");
                IntPtr owner = 0, text = 0;
                Check(Raw.KernelGetLibrary((IntPtr)(&owner), kernel)); Require(owner == library, "Kernel library owner.");
                Check(Raw.KernelGetName((IntPtr)(&text), kernel)); Require(Marshal.PtrToStringUTF8(text) == "Increment", "Kernel name.");
            }
            finally { Check(Raw.LibraryUnload(library)); }
        }
    }

    private static void DriverLink()
    {
        byte[] bitcode = Compile(true);
        for (int mode = 0; mode != 2; mode++)
        {
            IntPtr state = 0; if (!Capability(Raw.LinkCreate(0, 0, 0, (IntPtr)(&state)))) return;
            try
            {
                string path = Path.Combine(Environment.GetEnvironmentVariable("HIPSHARP_TEST_WORK")!, "driver-input.bc");
                using var name = new Utf8(mode == 0 ? "driver-input.bc" : path);
                if (mode == 0)
                {
                    fixed (byte* data = bitcode) Check(Raw.LinkAddData(state, 100, (IntPtr)data, (UIntPtr)bitcode.Length, name.Pointer, 0, 0, 0));
                }
                else { File.WriteAllBytes(path, bitcode); Check(Raw.LinkAddFile(state, 100, name.Pointer, 0, 0, 0)); }
                IntPtr binary = 0; UIntPtr length = 0; Check(Raw.LinkComplete(state, (IntPtr)(&binary), (IntPtr)(&length)));
                Require(binary != 0 && length > 16 && length < 64 * 1024 * 1024, "Driver linker output.");
                byte[] image = new byte[(int)length]; Marshal.Copy(binary, image, 0, image.Length); ExecuteImage(image, 0);
            }
            finally { Check(Raw.LinkDestroy(state)); }
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct KernelParameters
    {
        public HipDim3 Block;
        public IntPtr Extra, Function;
        public HipDim3 Grid;
        public IntPtr Arguments;
        public uint Shared;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DriverLaunch
    {
        public uint GridX, GridY, GridZ, BlockX, BlockY, BlockZ, Shared;
        public IntPtr Stream, Attributes;
        public uint AttributeCount;
    }

    private static void GraphKernel()
    {
        byte[] image = Compile(false); IntPtr module = 0, kernel = 0, pointer = 0, graph = 0, node = 0, second = 0, executable = 0;
        fixed (byte* data = image) Check(Raw.ModuleLoadData((IntPtr)(&module), (IntPtr)data));
        try
        {
            using var name = new Utf8("Increment"); Check(Raw.ModuleGetFunction((IntPtr)(&kernel), module, name.Pointer));
            Check(Raw.Malloc((IntPtr)(&pointer), sizeof(int))); int input = 0, output = -1;
            Check(Raw.MemcpyHtoD(pointer, (IntPtr)(&input), sizeof(int)));
            IntPtr argument = (IntPtr)(&pointer);
            KernelParameters parameters = new() { Block = new HipDim3(1), Grid = new HipDim3(1), Function = kernel, Arguments = (IntPtr)(&argument) };
            Check(Raw.GraphCreate((IntPtr)(&graph), 0));
            Check(Raw.GraphAddKernelNode((IntPtr)(&node), graph, 0, 0, (IntPtr)(&parameters)));
            Check(Raw.GraphAddKernelNode((IntPtr)(&second), graph, (IntPtr)(&node), 1, (IntPtr)(&parameters)));
            KernelParameters query = default; Check(Raw.GraphKernelNodeGetParams(node, (IntPtr)(&query)));
            Require(query.Function == kernel && query.Block.X == 1 && query.Grid.X == 1 && query.Arguments != 0, "Kernel node parameters.");
            Check(Raw.GraphKernelNodeSetParams(node, (IntPtr)(&parameters)));
            byte* attribute = stackalloc byte[64]; new Span<byte>(attribute, 64).Clear();
            if (Capability(Raw.GraphKernelNodeSetAttribute(node, 2, (IntPtr)attribute)))
            {
                Check(Raw.GraphKernelNodeCopyAttributes(node, second));
                *(int*)attribute = -1; Check(Raw.GraphKernelNodeGetAttribute(second, 2, (IntPtr)attribute)); Require(*(int*)attribute == 0, "Kernel attribute copy.");
            }
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphExecKernelNodeSetParams(executable, node, (IntPtr)(&parameters)));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize());
            Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == 14, "Graph kernels did not run in order.");
            DriverLaunch launch = new() { GridX = 1, GridY = 1, GridZ = 1, BlockX = 1, BlockY = 1, BlockZ = 1 };
            Check(Raw.DrvLaunchKernelEx((IntPtr)(&launch), kernel, (IntPtr)(&argument), 0));
            Check(Raw.DeviceSynchronize()); Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == 21, "Extended driver launch.");
            using var path = new Utf8(Path.Combine(Environment.GetEnvironmentVariable("HIPSHARP_TEST_WORK")!, "kernel-graph.dot"));
            Check(Raw.GraphDebugDotPrint(graph, path.Pointer, 0));
            Require(File.ReadAllText(Marshal.PtrToStringUTF8(path.Pointer)!).Contains("digraph"), "Graph DOT output.");
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); if (graph != 0) Check(Raw.GraphDestroy(graph));
            if (pointer != 0) Check(Raw.Free(pointer)); Check(Raw.ModuleUnload(module));
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct Position { public UIntPtr X, Y, Z; }
    [StructLayout(LayoutKind.Sequential)] private struct RuntimeCopy3D
    {
        public IntPtr SourceArray;
        public Position SourcePosition;
        public HipPitchedPtr Source;
        public IntPtr DestinationArray;
        public Position DestinationPosition;
        public HipPitchedPtr Destination;
        public HipExtent Extent;
        public int Kind;
    }

    private static void PitchedRuntime()
    {
        IntPtr two = 0, host = 0, stream = 0; UIntPtr pitch = 0; HipPitchedPtr three = default;
        Check(Raw.MallocPitch((IntPtr)(&two), (IntPtr)(&pitch), 64, 4)); Require(pitch >= 64, "Runtime pitch.");
        try
        {
            Check(Raw.HostMalloc((IntPtr)(&host), 256, 0)); Check(Raw.StreamCreate((IntPtr)(&stream)));
            Check(Raw.Memset2D(two, pitch, 0x19, 64, 4));
            Check(Raw.Memcpy2D(host, 64, two, pitch, 64, 4, 2));
            Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x19) == -1, "2D memset.");
            Check(Raw.Memset2DAsync(two, pitch, 0x2A, 64, 4, stream));
            Check(Raw.Memcpy2DAsync(host, 64, two, pitch, 64, 4, 2, stream)); Check(Raw.StreamSynchronize(stream));
            Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x2A) == -1, "2D async memset.");
            Check(Raw.Malloc3D((IntPtr)(&three), new HipExtent(64, 2, 2))); Require(three.Address != 0 && three.Pitch >= 64, "3D allocation.");
            RuntimeCopy3D download = new() { Source = three, Destination = new HipPitchedPtr(host, 64, 64, 2), Extent = new HipExtent(64, 2, 2), Kind = 2 };
            Check(Raw.Memset3D(three, 0x3B, new HipExtent(64, 2, 2)));
            Check(Raw.Memcpy3D((IntPtr)(&download))); Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x3B) == -1, "3D memset/copy.");
            Check(Raw.Memset3DAsync(three, 0x4C, new HipExtent(64, 2, 2), stream));
            Check(Raw.Memcpy3DAsync((IntPtr)(&download), stream)); Check(Raw.StreamSynchronize(stream));
            Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x4C) == -1, "3D async memset/copy.");
            for (int i = 0; i < 256; i++) ((byte*)host)[i] = (byte)(i * 37 + 11);
            Check(Raw.Memcpy(two, host, 256, 1)); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.MemcpyWithStream(host, two, 256, 2, stream)); Check(Raw.StreamSynchronize(stream)); VerifyPattern(host, 256);
            Check(Raw.MemcpyAsync(two, host, 256, 1, stream)); Check(Raw.StreamSynchronize(stream)); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.MemcpyAsync(host, two, 256, 2, stream)); Check(Raw.StreamSynchronize(stream)); VerifyPattern(host, 256);
        }
        finally
        {
            if (stream != 0) Check(Raw.StreamDestroy(stream)); if (three.Address != 0) Check(Raw.Free(three.Address));
            if (host != 0) Check(Raw.HostFree(host)); Check(Raw.Free(two));
        }
    }

    private static void ExtensionStream()
    {
        int units = 0; Check(Raw.DeviceGetAttribute((IntPtr)(&units), 63, 0)); Require(units > 0, "CU count.");
        int words = (units + 31) / 32; uint* mask = stackalloc uint[words]; uint* queried = stackalloc uint[words];
        new Span<uint>(mask, words).Clear(); mask[0] = 3;
        IntPtr stream = 0;
        if (!Capability(Raw.ExtStreamCreateWithCUMask((IntPtr)(&stream), (uint)words, (IntPtr)mask))) return;
        try
        {
            Check(Raw.ExtStreamGetCUMask(stream, (uint)words, (IntPtr)queried));
            for (int i = 0; i < words; i++) Require(mask[i] == queried[i], "CU mask round trip.");
            byte* attribute = stackalloc byte[64]; new Span<byte>(attribute, 64).Clear();
            if (Capability(Raw.StreamGetAttribute(stream, 3, (IntPtr)attribute)))
            {
                int original = *(int*)attribute; Check(Raw.StreamSetAttribute(stream, 3, (IntPtr)attribute));
                *(int*)attribute = -1; Check(Raw.StreamGetAttribute(stream, 3, (IntPtr)attribute)); Require(*(int*)attribute == original, "Stream attribute round trip.");
            }
            IntPtr pointer = 0; Check(Raw.ExtMallocWithFlags((IntPtr)(&pointer), 256, 0));
            try { Check(Raw.MemsetAsync(pointer, 0x68, 256, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 1, 0x68); }
            finally { Check(Raw.Free(pointer)); }
        }
        finally { Check(Raw.StreamDestroy(stream)); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct GraphAllocationParameters
    {
        public PoolProperties Pool;
        public IntPtr Access;
        public UIntPtr AccessCount, Size;
        public IntPtr Pointer;
    }
    private static void GraphAllocation()
    {
        IntPtr graph = 0, executable = 0, allocation = 0, free = 0, set = 0, host = 0, copy = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            GraphAllocationParameters parameters = new() { Pool = new PoolProperties { AllocationType = 1, Location = new HipMemLocation(1, 0) }, Size = 256 };
            if (!Capability(Raw.GraphAddMemAllocNode((IntPtr)(&allocation), graph, 0, 0, (IntPtr)(&parameters)))) return;
            Require(parameters.Pointer != 0, "Graph allocation address.");
            GraphAllocationParameters query = default; Check(Raw.GraphMemAllocNodeGetParams(allocation, (IntPtr)(&query)));
            Require(query.Pointer == parameters.Pointer && query.Size == 256, "Graph allocation params.");
            Check(Raw.HostMalloc((IntPtr)(&host), 256, 0));
            MemsetParameters memset = new() { Destination = parameters.Pointer, ElementSize = 1, Width = 256, Height = 1, Value = 0x6B };
            Check(Raw.GraphAddMemsetNode((IntPtr)(&set), graph, (IntPtr)(&allocation), 1, (IntPtr)(&memset)));
            Check(Raw.GraphAddMemcpyNode1D((IntPtr)(&copy), graph, (IntPtr)(&set), 1, host, parameters.Pointer, 256, 2));
            Check(Raw.GraphAddMemFreeNode((IntPtr)(&free), graph, (IntPtr)(&copy), 1, parameters.Pointer));
            IntPtr freed = 0; Check(Raw.GraphMemFreeNodeGetParams(free, (IntPtr)(&freed))); Require(freed == parameters.Pointer, "Graph free address.");
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x6B) == -1, "Graph allocation data.");
            ulong used = 0, reserved = 0;
            Check(Raw.DeviceGetGraphMemAttribute(0, 0, (IntPtr)(&used)));
            Check(Raw.DeviceGetGraphMemAttribute(0, 2, (IntPtr)(&reserved))); Require(reserved >= used, "Graph memory accounting.");
            ulong zero = 0; Check(Raw.DeviceSetGraphMemAttribute(0, 1, (IntPtr)(&zero)));
            Check(Raw.DeviceGraphMemTrim(0));
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); if (host != 0) Check(Raw.HostFree(host));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)] private struct GenericNode
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(16)] public MemsetParameters Memset;
    }
    private static void GenericGraph()
    {
        IntPtr pointer = 0, graph = 0, executable = 0, node = 0;
        Check(Raw.Malloc((IntPtr)(&pointer), 256)); Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            GenericNode parameters = new() { Type = 2, Memset = new MemsetParameters { Destination = pointer, ElementSize = 1, Height = 1, Width = 256, Value = 0x6C } };
            if (!Capability(Raw.GraphAddNode((IntPtr)(&node), graph, 0, 0, (IntPtr)(&parameters)))) return;
            parameters.Memset.Value = 0x7D; Check(Raw.GraphNodeSetParams(node, (IntPtr)(&parameters)));
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); VerifyFill(pointer, 1, 0x7D);
            parameters.Memset.Value = 0x8E; Check(Raw.GraphExecNodeSetParams(executable, node, (IntPtr)(&parameters)));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); VerifyFill(pointer, 1, 0x8E);
        }
        finally { if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); Check(Raw.Free(pointer)); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate HipError CountDevices(IntPtr count);
    private static void ProcAddress()
    {
        using var name = new Utf8("hipGetDeviceCount"); IntPtr pointer = 0; int status = -1, version = 0, count = 0, direct = 0;
        Check(Raw.RuntimeGetVersion((IntPtr)(&version))); Check(Raw.GetDeviceCount((IntPtr)(&direct)));
        if (Capability(Raw.GetProcAddress(name.Pointer, (IntPtr)(&pointer), version, 0, (IntPtr)(&status))))
        {
            Require(pointer != 0 && status == 0, "Proc address query result.");
            Require(Marshal.GetDelegateForFunctionPointer<CountDevices>(pointer)((IntPtr)(&count)) == HipError.Success && count == direct, "Resolved function behavior.");
        }
        pointer = 0; status = -1; count = 0;
        if (Capability(Raw.GetDriverEntryPoint(name.Pointer, (IntPtr)(&pointer), 0, (IntPtr)(&status))))
        {
            Require(pointer != 0 && status == 0, "Driver entry query result.");
            Require(Marshal.GetDelegateForFunctionPointer<CountDevices>(pointer)((IntPtr)(&count)) == HipError.Success && count == direct, "Driver-resolved function behavior.");
        }
    }
}
