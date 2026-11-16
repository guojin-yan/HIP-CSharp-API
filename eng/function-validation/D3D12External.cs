using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp;

internal static unsafe partial class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D3DCreate(out IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void D3DDestroy(IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D3DHandles(IntPtr context, out IntPtr memory, out IntPtr fence);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong D3DValue(IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D3DSignal(IntPtr context, ulong value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D3DObserve(IntPtr context, ulong expectedFence, ulong nextFence, byte value);
    private sealed class D3DContext : IDisposable
    {
        internal IntPtr Context, Memory, Fence;
        internal readonly D3DValue Value;
        internal readonly D3DSignal Signal;
        internal readonly D3DObserve Observe;
        private readonly D3DDestroy _destroy;
        internal D3DContext()
        {
            Require(OperatingSystem.IsWindows(), "D3D12 fixture requires Windows.");
            int count = 0, current = -1;
            Check(Raw.GetDeviceCount((IntPtr)(&count))); Check(Raw.GetDevice((IntPtr)(&current)));
            Require(count == 1 && current == 0, "D3D12 fixture currently requires one HIP device; multi-adapter matching is not inferred.");
            IntPtr table = LoadHelperPointer("D3D12Functions");
            T Function<T>(int index) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(table, index * IntPtr.Size));
            D3DCreate create = Function<D3DCreate>(0);
            _destroy = Function<D3DDestroy>(1);
            D3DHandles handles = Function<D3DHandles>(2);
            Value = Function<D3DValue>(3); Signal = Function<D3DSignal>(4); Observe = Function<D3DObserve>(5);
            D3DCheck(create(out Context), "Create shared D3D12 resources");
            try { D3DCheck(handles(Context, out Memory, out Fence), "Get shared handles"); Require(Context != 0 && Memory != 0 && Fence != 0, "D3D12 handles."); }
            catch { Dispose(); throw; }
        }
        public void Dispose() { if (Context != 0) { _destroy(Context); Context = 0; } }
    }
    private static void D3DCheck(int result, string operation) => Require(result >= 0, $"{operation}: HRESULT 0x{result:x8}.");
    [StructLayout(LayoutKind.Explicit, Size = 104)] private struct ExternalMemoryDescriptor
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(8)] public IntPtr Handle;
        [FieldOffset(24)] public ulong Size;
        [FieldOffset(32)] public uint Flags;
    }
    [StructLayout(LayoutKind.Explicit, Size = 88)] private struct ExternalBufferDescriptor
    {
        [FieldOffset(0)] public ulong Offset;
        [FieldOffset(8)] public ulong Size;
        [FieldOffset(16)] public uint Flags;
    }
    [StructLayout(LayoutKind.Explicit, Size = 96)] private struct ExternalSemaphoreDescriptor
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(8)] public IntPtr Handle;
    }
    [StructLayout(LayoutKind.Explicit, Size = 144)] private struct ExternalFenceParameters
    {
        [FieldOffset(0)] public ulong Value;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ExternalSemaphoreNode
    {
        public IntPtr Semaphores, Parameters;
        public uint Count;
    }
    private static void D3D12ExternalMemory()
    {
        using var d3d = new D3DContext(); IntPtr memory = 0, pointer = 0;
        ExternalMemoryDescriptor descriptor = new() { Type = 5, Handle = d3d.Memory, Size = 65536, Flags = 1 };
        Check(Raw.ImportExternalMemory((IntPtr)(&memory), (IntPtr)(&descriptor)));
        Require(memory != 0, "Imported D3D12 resource.");
        try
        {
            ExternalBufferDescriptor buffer = new() { Size = 4096 };
            Check(Raw.ExternalMemoryGetMappedBuffer((IntPtr)(&pointer), memory, (IntPtr)(&buffer)));
            Require(pointer != 0, "Mapped external buffer.");
            Check(Raw.Memset(pointer, 0x5B, 4096));
            byte* actual = stackalloc byte[4096]; Check(Raw.MemcpyDtoH((IntPtr)actual, pointer, 4096));
            Require(new ReadOnlySpan<byte>(actual, 4096).IndexOfAnyExcept((byte)0x5B) == -1, "HIP shared bytes.");
            D3DCheck(d3d.Observe(d3d.Context, 0, 1, 0x5B), "Independent D3D12 GPU copy/readback");
            Console.WriteLine("D3D12_SHARED_BYTES=passed");
        }
        finally { if (pointer != 0) Check(Raw.Free(pointer)); Check(Raw.DestroyExternalMemory(memory)); }
    }
    private static void D3D12ManagedMemory()
    {
        using var d3d = new D3DContext();
        using var runtime = new HipRuntime(); runtime.Initialize();
        ExternalMemoryDescriptor descriptor = new() { Type = 5, Handle = d3d.Memory, Size = 65536, Flags = 1 };
        using var imported = runtime.AdvancedInterop.ImportExternalMemory(new HipNativeDescriptor((IntPtr)(&descriptor)));
        ExternalBufferDescriptor buffer = new() { Size = 4096 };
        using var mapping = imported.MapBuffer(new HipNativeDescriptor((IntPtr)(&buffer)));
        imported.Dispose(); Require(imported.IsDisposed, "Managed parent disposal requested.");
        IntPtr pointer = mapping.DangerousGetHandle();
        Check(Raw.Memset(pointer, 0x6C, 4096));
        Check(Raw.DeviceSynchronize()); // Complete HIP writes before handing the resource to D3D12.
        D3DCheck(d3d.Observe(d3d.Context, 0, 1, 0x6C), "Mapped buffer retains disposed parent; D3D12 readback");
        mapping.Dispose(); Require(mapping.IsDisposed, "Managed mapping released.");
        bool rejected = false;
        try { mapping.DangerousGetHandle(); } catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "Disposed external buffer rejects use.");
        Console.WriteLine("D3D12_MANAGED_PARENT_LIFETIME=passed");
    }
    private static void D3D12ManagedSemaphore()
    {
        using var d3d = new D3DContext();
        using var runtime = new HipRuntime(); runtime.Initialize();
        using var stream = runtime.CreateStream();
        ExternalSemaphoreDescriptor descriptor = new() { Type = 4, Handle = d3d.Fence };
        using var semaphore = runtime.AdvancedInterop.ImportExternalSemaphore(new HipNativeDescriptor((IntPtr)(&descriptor)));
        ExternalFenceParameters signal = new() { Value = 1 }, wait = new() { Value = 2 };
        runtime.AdvancedInterop.SignalExternalSemaphores(stream, new[] { semaphore }, new HipNativeDescriptor((IntPtr)(&signal)));
        stream.Synchronize(); Require(d3d.Value(d3d.Context) >= 1, "Managed HIP signal visible to D3D12.");
        runtime.AdvancedInterop.WaitExternalSemaphores(stream, new[] { semaphore }, new HipNativeDescriptor((IntPtr)(&wait)));
        semaphore.Dispose(); Require(semaphore.IsDisposed, "Semaphore owner disposal requested during pending wait.");
        D3DCheck(d3d.Signal(d3d.Context, 2), "Release managed pending fence");
        stream.Synchronize(); // Pending borrow releases the disposed import only after completion.
        Console.WriteLine("D3D12_MANAGED_SEMAPHORE_LIFETIME=passed");
    }
    private static void D3D12ExternalSemaphore(bool graphMode)
    {
        using var d3d = new D3DContext(); IntPtr semaphore = 0, stream = 0, completion = 0;
        ExternalSemaphoreDescriptor descriptor = new() { Type = 4, Handle = d3d.Fence };
        Check(Raw.ImportExternalSemaphore((IntPtr)(&semaphore), (IntPtr)(&descriptor)));
        Require(semaphore != 0, "Imported D3D12 fence.");
        try
        {
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            if (graphMode) { D3D12ExternalGraph(d3d, semaphore, stream); return; }
            ExternalFenceParameters signal = new() { Value = 1 }, wait = new() { Value = 2 };
            Check(Raw.SignalExternalSemaphoresAsync((IntPtr)(&semaphore), (IntPtr)(&signal), 1, stream));
            Check(Raw.StreamSynchronize(stream)); Require(d3d.Value(d3d.Context) >= 1, "HIP signal visible in D3D12.");
            D3DCheck(d3d.Signal(d3d.Context, 2), "D3D12 queue signal");
            Check(Raw.WaitExternalSemaphoresAsync((IntPtr)(&semaphore), (IntPtr)(&wait), 1, stream));
            Check(Raw.StreamSynchronize(stream));
            // Check an unsignaled future fence prevents the following event from completing.
            wait.Value = 3;
            Check(Raw.EventCreateWithFlags((IntPtr)(&completion), 2));
            Check(Raw.WaitExternalSemaphoresAsync((IntPtr)(&semaphore), (IntPtr)(&wait), 1, stream));
            Check(Raw.EventRecord(completion, stream));
            try { Check(Raw.EventQuery(completion), JYPPX.ROCm.HipSharp.Types.HipError.NotReady); }
            finally { D3DCheck(d3d.Signal(d3d.Context, 3), "Release future D3D12 fence"); }
            Check(Raw.EventSynchronize(completion)); Check(Raw.StreamSynchronize(stream));
            Console.WriteLine("D3D12_BIDIRECTIONAL_FENCE=passed");
        }
        finally { if (completion != 0) Check(Raw.EventDestroy(completion)); if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.DestroyExternalSemaphore(semaphore)); }
    }
    private static void D3D12ExternalGraph(D3DContext d3d, IntPtr semaphore, IntPtr stream)
    {
        IntPtr graph = 0, executable = 0, waitNode = 0, signalNode = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            ExternalFenceParameters wait = new() { Value = 1 }, signal = new() { Value = 2 };
            ExternalSemaphoreNode waitParameters = new() { Semaphores = (IntPtr)(&semaphore), Parameters = (IntPtr)(&wait), Count = 1 };
            ExternalSemaphoreNode signalParameters = new() { Semaphores = (IntPtr)(&semaphore), Parameters = (IntPtr)(&signal), Count = 1 };
            Check(Raw.GraphAddExternalSemaphoresWaitNode((IntPtr)(&waitNode), graph, 0, 0, (IntPtr)(&waitParameters)));
            Check(Raw.GraphAddExternalSemaphoresSignalNode((IntPtr)(&signalNode), graph, (IntPtr)(&waitNode), 1, (IntPtr)(&signalParameters)));
            ExternalSemaphoreNode queried = default;
            Check(Raw.GraphExternalSemaphoresWaitNodeGetParams(waitNode, (IntPtr)(&queried)));
            Require(queried.Count == 1 && *(IntPtr*)queried.Semaphores == semaphore && ((ExternalFenceParameters*)queried.Parameters)->Value == 1, "Graph fence wait query.");
            Check(Raw.GraphExternalSemaphoresSignalNodeGetParams(signalNode, (IntPtr)(&queried)));
            Require(queried.Count == 1 && *(IntPtr*)queried.Semaphores == semaphore && ((ExternalFenceParameters*)queried.Parameters)->Value == 2, "Graph fence signal query.");
            wait.Value = 3; signal.Value = 4;
            Check(Raw.GraphExternalSemaphoresWaitNodeSetParams(waitNode, (IntPtr)(&waitParameters)));
            Check(Raw.GraphExternalSemaphoresSignalNodeSetParams(signalNode, (IntPtr)(&signalParameters)));
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            D3DCheck(d3d.Signal(d3d.Context, 3), "D3D12 initial graph fence");
            Check(Raw.GraphLaunch(executable, stream)); Check(Raw.StreamSynchronize(stream));
            Require(d3d.Value(d3d.Context) >= 4, "Graph node updates signal D3D12 fence.");
            wait.Value = 5; signal.Value = 6;
            Check(Raw.GraphExecExternalSemaphoresWaitNodeSetParams(executable, waitNode, (IntPtr)(&waitParameters)));
            Check(Raw.GraphExecExternalSemaphoresSignalNodeSetParams(executable, signalNode, (IntPtr)(&signalParameters)));
            D3DCheck(d3d.Signal(d3d.Context, 5), "D3D12 updated graph fence");
            Check(Raw.GraphLaunch(executable, stream)); Check(Raw.StreamSynchronize(stream));
            Require(d3d.Value(d3d.Context) >= 6, "Executable node updates signal D3D12 fence.");
            Console.WriteLine("D3D12_GRAPH_FENCE_UPDATES=passed");
        }
        finally { if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); }
    }
}
