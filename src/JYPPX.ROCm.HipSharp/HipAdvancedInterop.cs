using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

#pragma warning disable CS1591 // Public surface is documented in the advanced-interop guide.
#pragma warning disable CA1720 // Native ABI terminology intentionally uses pointer/handle names.

namespace JYPPX.ROCm.HipSharp;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void HipStreamNativeCallback(IntPtr stream, HipError status, IntPtr userData);

/// <summary>
/// ABI 专用 HIP descriptor 的借用指针，descriptor 存储由调用方负责 / A borrowed pointer to an ABI-specific HIP descriptor; the caller owns the descriptor storage.
/// </summary>
public readonly struct HipNativeDescriptor
{
    /// <summary>创建借用 descriptor 包装 / Creates a borrowed descriptor wrapper.</summary>
    public HipNativeDescriptor(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) throw new ArgumentOutOfRangeException(nameof(pointer));
        Pointer = pointer;
    }

    /// <summary>获取借用的原生 descriptor 指针 / Gets the borrowed native descriptor pointer.</summary>
    public IntPtr Pointer { get; }
}

/// <summary>管理 HIP 外部内存导入的所有权 / Owns a HIP external-memory import.</summary>
public sealed class HipExternalMemory : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private IntPtr _handle;

    internal HipExternalMemory(IHipNativeApi nativeApi, IntPtr handle)
    {
        _nativeApi = nativeApi;
        _handle = handle;
    }

    /// <summary>指示对象是否已释放 / Indicates whether the object has been disposed.</summary>
    public bool IsDisposed => _handle == IntPtr.Zero;

    /// <summary>获取原生 external memory handle / Gets the native external-memory handle.</summary>
    public IntPtr DangerousGetHandle()
    {
        IntPtr handle = _handle;
        if (handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(HipExternalMemory));
        return handle;
    }

    /// <summary>映射外部内存缓冲区 / Maps an external-memory buffer.</summary>
    public HipExternalMemoryBuffer MapBuffer(HipNativeDescriptor descriptor)
    {
        IntPtr output = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(output, IntPtr.Zero);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ExternalMemoryGetMappedBuffer(output, DangerousGetHandle(), descriptor.Pointer), "hipExternalMemoryGetMappedBuffer");
            IntPtr pointer = Marshal.ReadIntPtr(output);
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("hipExternalMemoryGetMappedBuffer succeeded but returned a null pointer.");
            return new HipExternalMemoryBuffer(_nativeApi, pointer);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>映射外部 mipmapped array / Maps an external mipmapped array.</summary>
    public HipExternalMipmappedArray MapMipmappedArray(HipNativeDescriptor descriptor)
    {
        IntPtr output = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(output, IntPtr.Zero);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ExternalMemoryGetMappedMipmappedArray(output, DangerousGetHandle(), descriptor.Pointer), "hipExternalMemoryGetMappedMipmappedArray");
            IntPtr handle = Marshal.ReadIntPtr(output);
            if (handle == IntPtr.Zero) throw new InvalidOperationException("hipExternalMemoryGetMappedMipmappedArray succeeded but returned a null handle.");
            return new HipExternalMipmappedArray(_nativeApi, handle);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>释放外部内存导入 / Releases the external-memory import.</summary>
    public void Dispose()
    {
        IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DestroyExternalMemory(handle), "hipDestroyExternalMemory");
    }
}

/// <summary>管理映射的外部内存缓冲区，并通过 <c>hipFree</c> 释放 / Owns a mapped external-memory buffer and releases it with <c>hipFree</c>.</summary>
public sealed class HipExternalMemoryBuffer : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private IntPtr _pointer;
    internal HipExternalMemoryBuffer(IHipNativeApi nativeApi, IntPtr pointer) { _nativeApi = nativeApi; _pointer = pointer; }
    /// <summary>指示对象是否已释放 / Indicates whether the object has been disposed.</summary>
    public bool IsDisposed => _pointer == IntPtr.Zero;
    /// <summary>获取映射缓冲区指针 / Gets the mapped buffer pointer.</summary>
    public IntPtr DangerousGetHandle() => _pointer != IntPtr.Zero ? _pointer : throw new ObjectDisposedException(nameof(HipExternalMemoryBuffer));
    /// <summary>释放映射缓冲区 / Releases the mapped buffer.</summary>
    public void Dispose()
    {
        IntPtr pointer = Interlocked.Exchange(ref _pointer, IntPtr.Zero);
        if (pointer != IntPtr.Zero) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.Free(pointer), "hipFree(external-memory-buffer)");
    }
}

/// <summary>管理映射的外部 mipmapped array / Owns a mapped external mipmapped array.</summary>
public sealed class HipExternalMipmappedArray : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private IntPtr _handle;
    internal HipExternalMipmappedArray(IHipNativeApi nativeApi, IntPtr handle) { _nativeApi = nativeApi; _handle = handle; }
    /// <summary>指示对象是否已释放 / Indicates whether the object has been disposed.</summary>
    public bool IsDisposed => _handle == IntPtr.Zero;
    /// <summary>获取 mipmapped array handle / Gets the mipmapped-array handle.</summary>
    public IntPtr DangerousGetHandle() => _handle != IntPtr.Zero ? _handle : throw new ObjectDisposedException(nameof(HipExternalMipmappedArray));
    /// <summary>释放 mipmapped array / Releases the mipmapped array.</summary>
    public void Dispose()
    {
        IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.FreeMipmappedArray(handle), "hipFreeMipmappedArray(external-memory)");
    }
}

/// <summary>管理 HIP 外部 semaphore 导入的所有权 / Owns a HIP external-semaphore import.</summary>
public sealed class HipExternalSemaphore : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private IntPtr _handle;
    internal HipExternalSemaphore(IHipNativeApi nativeApi, IntPtr handle) { _nativeApi = nativeApi; _handle = handle; }
    /// <summary>指示对象是否已释放 / Indicates whether the object has been disposed.</summary>
    public bool IsDisposed => _handle == IntPtr.Zero;
    /// <summary>获取 external semaphore handle / Gets the external-semaphore handle.</summary>
    public IntPtr DangerousGetHandle() => _handle != IntPtr.Zero ? _handle : throw new ObjectDisposedException(nameof(HipExternalSemaphore));
    /// <summary>释放 external semaphore / Releases the external semaphore.</summary>
    public void Dispose()
    {
        IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DestroyExternalSemaphore(handle), "hipDestroyExternalSemaphore");
    }
}

/// <summary>管理由图形 API 注册调用返回的 graphics resource / Owns a graphics resource returned by a graphics API registration call.</summary>
public sealed class HipGraphicsResource : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private IntPtr _handle;
    internal HipGraphicsResource(IHipNativeApi nativeApi, IntPtr handle) { _nativeApi = nativeApi; _handle = handle; }
    /// <summary>指示对象是否已释放 / Indicates whether the object has been disposed.</summary>
    public bool IsDisposed => _handle == IntPtr.Zero;
    internal IHipNativeApi NativeApi => _nativeApi;
    /// <summary>获取 graphics resource handle / Gets the graphics-resource handle.</summary>
    public IntPtr DangerousGetHandle() => _handle != IntPtr.Zero ? _handle : throw new ObjectDisposedException(nameof(HipGraphicsResource));
    /// <summary>注销 graphics resource / Unregisters the graphics resource.</summary>
    public void Dispose()
    {
        IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsUnregisterResource(handle), "hipGraphicsUnregisterResource");
    }
}

/// <summary>表示映射到 stream 的资源，释放时会入队取消映射 / Represents resources mapped on a stream; disposing it enqueues their unmap.</summary>
public sealed class HipGraphicsMapping : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipGraphicsResource[] _resources;
    private readonly HipStream _stream;
    private int _disposed;
    internal HipGraphicsMapping(IHipNativeApi nativeApi, HipGraphicsResource[] resources, HipStream stream) { _nativeApi = nativeApi; _resources = resources; _stream = stream; }

    /// <summary>获取映射 graphics resource 的 device pointer / Gets the device pointer for a mapped graphics resource.</summary>
    public HipMappedGraphicsPointer GetMappedPointer(HipGraphicsResource resource)
    {
        ValidateResource(resource);
        IntPtr pointer = Marshal.AllocHGlobal(IntPtr.Size);
        IntPtr size = Marshal.AllocHGlobal(UIntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(pointer, IntPtr.Zero);
            if (UIntPtr.Size == 4) Marshal.WriteInt32(size, 0); else Marshal.WriteInt64(size, 0);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsResourceGetMappedPointer(pointer, size, resource.DangerousGetHandle()), "hipGraphicsResourceGetMappedPointer");
            return new HipMappedGraphicsPointer(Marshal.ReadIntPtr(pointer), UIntPtr.Size == 4 ? unchecked((uint)Marshal.ReadInt32(size)) : unchecked((ulong)Marshal.ReadInt64(size)));
        }
        finally { Marshal.FreeHGlobal(size); Marshal.FreeHGlobal(pointer); }
    }

    /// <summary>获取映射 graphics resource 的 array handle / Gets the array handle for a mapped graphics resource.</summary>
    public IntPtr GetMappedArray(HipGraphicsResource resource, uint arrayIndex, uint mipLevel)
    {
        ValidateResource(resource);
        IntPtr output = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(output, IntPtr.Zero);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsSubResourceGetMappedArray(output, resource.DangerousGetHandle(), arrayIndex, mipLevel), "hipGraphicsSubResourceGetMappedArray");
            return Marshal.ReadIntPtr(output);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>取消 stream 上资源的映射 / Unmaps the resources from the stream.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        HipAdvancedInterop.WithHandles(_resources, handles => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsUnmapResources(_resources.Length, handles, _stream.DangerousGetHandle()), "hipGraphicsUnmapResources"));
    }

    private void ValidateResource(HipGraphicsResource resource)
    {
        if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(HipGraphicsMapping));
        if (resource is null) throw new ArgumentNullException(nameof(resource));
        if (!ReferenceEquals(resource.NativeApi, _nativeApi) || Array.IndexOf(_resources, resource) < 0) throw new ArgumentException("Resource is not part of this mapping.", nameof(resource));
    }
}

/// <summary>来自映射 graphics resource 的借用 device pointer 和字节长度 / A borrowed device pointer and byte length from a mapped graphics resource.</summary>
public readonly struct HipMappedGraphicsPointer
{
    internal HipMappedGraphicsPointer(IntPtr pointer, ulong byteLength) { Pointer = pointer; ByteLength = byteLength; }
    /// <summary>获取 device pointer / Gets the device pointer.</summary>
    public IntPtr Pointer { get; }
    /// <summary>获取缓冲区字节长度 / Gets the buffer byte length.</summary>
    public ulong ByteLength { get; }
}

/// <summary>管理由 HIP IPC memory handle 打开的 device pointer / Owns a device pointer opened from a HIP IPC memory handle.</summary>
public sealed class HipIpcMemory : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private IntPtr _pointer;
    internal HipIpcMemory(IHipNativeApi nativeApi, IntPtr pointer) { _nativeApi = nativeApi; _pointer = pointer; }
    /// <summary>指示对象是否已释放 / Indicates whether the object has been disposed.</summary>
    public bool IsDisposed => _pointer == IntPtr.Zero;
    /// <summary>获取 IPC device pointer / Gets the IPC device pointer.</summary>
    public IntPtr DangerousGetHandle() => _pointer != IntPtr.Zero ? _pointer : throw new ObjectDisposedException(nameof(HipIpcMemory));
    /// <summary>关闭 IPC memory handle / Closes the IPC memory handle.</summary>
    public void Dispose()
    {
        IntPtr pointer = Interlocked.Exchange(ref _pointer, IntPtr.Zero);
        if (pointer != IntPtr.Zero) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.IpcCloseMemHandle(pointer), "hipIpcCloseMemHandle");
    }
}

/// <summary>管理 HIP graph user object 的一个引用 / Owns one reference to a HIP graph user object.</summary>
public sealed class HipUserObject : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private IntPtr _handle;
    internal HipUserObject(IHipNativeApi nativeApi, IntPtr handle) { _nativeApi = nativeApi; _handle = handle; }
    /// <summary>指示对象是否已释放 / Indicates whether the object has been disposed.</summary>
    public bool IsDisposed => _handle == IntPtr.Zero;
    internal IHipNativeApi NativeApi => _nativeApi;
    internal IntPtr DangerousGetHandle() => _handle != IntPtr.Zero ? _handle : throw new ObjectDisposedException(nameof(HipUserObject));
    /// <summary>增加 user object 引用计数 / Retains references to the user object.</summary>
    public void Retain(uint count = 1)
    {
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.UserObjectRetain(DangerousGetHandle(), count), "hipUserObjectRetain");
    }
    /// <summary>释放 user object 引用 / Releases the owned user-object reference.</summary>
    public void Dispose()
    {
        IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.UserObjectRelease(handle, 1), "hipUserObjectRelease");
    }
}

/// <summary>释放时停止 HIP profiling / Stops HIP profiling when disposed.</summary>
public sealed class HipProfilerSession : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private int _disposed;
    internal HipProfilerSession(IHipNativeApi nativeApi) { _nativeApi = nativeApi; }
    /// <summary>指示 profiling 是否已停止 / Indicates whether profiling has been stopped.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    /// <summary>停止 profiling / Stops profiling.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ProfilerStop(), "hipProfilerStop");
    }
}

/// <summary>高级 graph interop API 返回的借用 node 标识 / Borrowed node identity returned by advanced graph interop APIs.</summary>
public sealed class HipAdvancedGraphNode
{
    internal HipAdvancedGraphNode(HipGraph graph, IntPtr handle) { Graph = graph; Handle = handle; }
    /// <summary>获取所属 graph / Gets the owning graph.</summary>
    public HipGraph Graph { get; }
    /// <summary>指示 node 是否仍有效 / Indicates whether the node is still valid.</summary>
    public bool IsValid => !Graph.IsDisposed;
    internal IntPtr Handle { get; }
}

/// <summary>HIP Runtime 外部资源、IPC、callback、profiler 与兼容性部分的托管门面 / Managed facade for the external, IPC, callback, profiler, and compatibility portions of HIP Runtime.</summary>
public sealed class HipAdvancedInterop
{
    private static readonly HipStreamNativeCallback s_streamCallback = InvokeStreamCallback;
    private static readonly IntPtr s_streamCallbackPointer = Marshal.GetFunctionPointerForDelegate(s_streamCallback);
    private readonly HipRuntime _runtime;
    private readonly IHipNativeApi _nativeApi;

    internal HipAdvancedInterop(HipRuntime runtime, IHipNativeApi nativeApi) { _runtime = runtime; _nativeApi = nativeApi; }

    /// <summary>导入外部 memory descriptor / Imports an external-memory descriptor.</summary>
    public HipExternalMemory ImportExternalMemory(HipNativeDescriptor descriptor)
    {
        IntPtr output = AllocateOutputHandle();
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ImportExternalMemory(output, descriptor.Pointer), "hipImportExternalMemory");
            return new HipExternalMemory(_nativeApi, ReadOutputHandle(output, "hipImportExternalMemory"));
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>导入外部 semaphore descriptor / Imports an external-semaphore descriptor.</summary>
    public HipExternalSemaphore ImportExternalSemaphore(HipNativeDescriptor descriptor)
    {
        IntPtr output = AllocateOutputHandle();
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ImportExternalSemaphore(output, descriptor.Pointer), "hipImportExternalSemaphore");
            return new HipExternalSemaphore(_nativeApi, ReadOutputHandle(output, "hipImportExternalSemaphore"));
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>接管已注册的 graphics resource handle / Takes ownership of a registered graphics-resource handle.</summary>
    public HipGraphicsResource OwnGraphicsResource(IntPtr nativeHandle)
    {
        _runtime.ThrowIfDisposedInternal();
        if (nativeHandle == IntPtr.Zero) throw new ArgumentOutOfRangeException(nameof(nativeHandle));
        return new HipGraphicsResource(_nativeApi, nativeHandle);
    }

    /// <summary>在 stream 上映射 graphics resources / Maps graphics resources on a stream.</summary>
    public HipGraphicsMapping MapGraphicsResources(IReadOnlyList<HipGraphicsResource> resources, HipStream stream)
    {
        if (resources is null) throw new ArgumentNullException(nameof(resources));
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (resources.Count == 0) throw new ArgumentOutOfRangeException(nameof(resources));
        if (!ReferenceEquals(stream.NativeApi, _nativeApi)) throw new ArgumentException("Stream belongs to another HIP Runtime client.", nameof(stream));
        HipGraphicsResource[] copy = new HipGraphicsResource[resources.Count];
        for (int index = 0; index < copy.Length; index++)
        {
            HipGraphicsResource resource = resources[index] ?? throw new ArgumentException("Resources cannot contain null values.", nameof(resources));
            if (!ReferenceEquals(resource.NativeApi, _nativeApi)) throw new ArgumentException("Resource belongs to another HIP Runtime client.", nameof(resources));
            copy[index] = resource;
        }
        WithHandles(copy, handles => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsMapResources(copy.Length, handles, stream.DangerousGetHandle()), "hipGraphicsMapResources"));
        return new HipGraphicsMapping(_nativeApi, copy, stream);
    }

    /// <summary>获取 device memory 的 IPC handle / Gets an IPC handle for device memory.</summary>
    public HipIpcMemHandle GetIpcMemoryHandle(HipDeviceMemory memory)
    {
        if (memory is null) throw new ArgumentNullException(nameof(memory));
        if (!ReferenceEquals(memory.NativeApi, _nativeApi)) throw new ArgumentException("Memory belongs to another HIP Runtime client.", nameof(memory));
        IntPtr output = Marshal.AllocHGlobal(Marshal.SizeOf<HipIpcMemHandle>());
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.IpcGetMemHandle(output, memory.DangerousGetHandle()), "hipIpcGetMemHandle");
            return Marshal.PtrToStructure<HipIpcMemHandle>(output);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>获取 event 的 IPC handle / Gets an IPC handle for an event.</summary>
    public HipIpcEventHandle GetIpcEventHandle(HipEvent eventHandle)
    {
        if (eventHandle is null) throw new ArgumentNullException(nameof(eventHandle));
        if (!ReferenceEquals(eventHandle.NativeApi, _nativeApi)) throw new ArgumentException("Event belongs to another HIP Runtime client.", nameof(eventHandle));
        IntPtr output = Marshal.AllocHGlobal(Marshal.SizeOf<HipIpcEventHandle>());
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.IpcGetEventHandle(output, eventHandle.DangerousGetHandle()), "hipIpcGetEventHandle");
            return Marshal.PtrToStructure<HipIpcEventHandle>(output);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>打开 IPC memory handle / Opens an IPC memory handle.</summary>
    public HipIpcMemory OpenIpcMemory(HipIpcMemHandle handle, uint flags = 0)
    {
        IntPtr output = AllocateOutputHandle();
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.IpcOpenMemHandle(output, handle, flags), "hipIpcOpenMemHandle");
            return new HipIpcMemory(_nativeApi, ReadOutputHandle(output, "hipIpcOpenMemHandle"));
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>打开 IPC event handle / Opens an IPC event handle.</summary>
    public HipEvent OpenIpcEvent(HipIpcEventHandle handle)
    {
        IntPtr output = AllocateOutputHandle();
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.IpcOpenEventHandle(output, handle), "hipIpcOpenEventHandle");
            return new HipEvent(_nativeApi, ReadOutputHandle(output, "hipIpcOpenEventHandle"), HipEventFlags.Default);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>在 stream 上异步 signal external semaphores / Asynchronously signals external semaphores on a stream.</summary>
    public void SignalExternalSemaphores(HipStream stream, IReadOnlyList<HipExternalSemaphore> semaphores, HipNativeDescriptor parameters) =>
        EnqueueExternalSemaphores(stream, semaphores, parameters, true);

    /// <summary>在 stream 上异步等待 external semaphores / Asynchronously waits for external semaphores on a stream.</summary>
    public void WaitExternalSemaphores(HipStream stream, IReadOnlyList<HipExternalSemaphore> semaphores, HipNativeDescriptor parameters) =>
        EnqueueExternalSemaphores(stream, semaphores, parameters, false);

    /// <summary>向 stream 注册托管 callback / Registers a managed callback on a stream.</summary>
    public void AddStreamCallback(HipStream stream, Action<HipError> callback, uint flags = 0)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (callback is null) throw new ArgumentNullException(nameof(callback));
        if (!ReferenceEquals(stream.NativeApi, _nativeApi)) throw new ArgumentException("Stream belongs to another HIP Runtime client.", nameof(stream));
        GCHandle state = GCHandle.Alloc(callback);
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamAddCallback(stream.DangerousGetHandle(), s_streamCallbackPointer, GCHandle.ToIntPtr(state), flags), "hipStreamAddCallback");
            state = default;
        }
        finally { if (state.IsAllocated) state.Free(); }
    }

    /// <summary>启动 HIP profiler 会话 / Starts a HIP profiler session.</summary>
    public HipProfilerSession StartProfiler()
    {
        _runtime.ThrowIfDisposedInternal();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ProfilerStart(), "hipProfilerStart");
        return new HipProfilerSession(_nativeApi);
    }

    /// <summary>创建 graph user object / Creates a graph user object.</summary>
    public HipUserObject CreateUserObject(IntPtr value, HipNativeDescriptor destroyCallback, uint initialReferenceCount = 1, uint flags = 0)
    {
        if (initialReferenceCount == 0) throw new ArgumentOutOfRangeException(nameof(initialReferenceCount));
        IntPtr output = AllocateOutputHandle();
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.UserObjectCreate(output, value, destroyCallback.Pointer, initialReferenceCount, flags), "hipUserObjectCreate");
            return new HipUserObject(_nativeApi, ReadOutputHandle(output, "hipUserObjectCreate"));
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>在 graph 中保留 user object / Retains a user object in a graph.</summary>
    public void RetainUserObject(HipGraph graph, HipUserObject userObject, uint count = 1, uint flags = 0)
    {
        ValidateGraphUserObject(graph, userObject, count);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphRetainUserObject(graph.DangerousGetHandle(), userObject.DangerousGetHandle(), count, flags), "hipGraphRetainUserObject");
    }

    /// <summary>在 graph 中释放 user object 引用 / Releases user-object references from a graph.</summary>
    public void ReleaseUserObject(HipGraph graph, HipUserObject userObject, uint count = 1)
    {
        ValidateGraphUserObject(graph, userObject, count);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphReleaseUserObject(graph.DangerousGetHandle(), userObject.DangerousGetHandle(), count), "hipGraphReleaseUserObject");
    }

    /// <summary>添加 external semaphore signal graph node / Adds an external-semaphore signal graph node.</summary>
    public HipAdvancedGraphNode AddExternalSemaphoreSignalNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, HipNativeDescriptor parameters) =>
        AddAdvancedGraphNode(graph, dependencies, parameters, true, false, false);

    /// <summary>添加 external semaphore wait graph node / Adds an external-semaphore wait graph node.</summary>
    public HipAdvancedGraphNode AddExternalSemaphoreWaitNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, HipNativeDescriptor parameters) =>
        AddAdvancedGraphNode(graph, dependencies, parameters, false, false, false);

    /// <summary>设置 external semaphore signal node 参数 / Sets external-semaphore signal node parameters.</summary>
    public void SetExternalSemaphoreSignalNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor parameters) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExternalSemaphoresSignalNodeSetParams(ValidateNode(node), parameters.Pointer), "hipGraphExternalSemaphoresSignalNodeSetParams");

    /// <summary>读取 external semaphore signal node 参数 / Gets external-semaphore signal node parameters.</summary>
    public void GetExternalSemaphoreSignalNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor destination) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExternalSemaphoresSignalNodeGetParams(ValidateNode(node), destination.Pointer), "hipGraphExternalSemaphoresSignalNodeGetParams");

    /// <summary>设置 external semaphore wait node 参数 / Sets external-semaphore wait node parameters.</summary>
    public void SetExternalSemaphoreWaitNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor parameters) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExternalSemaphoresWaitNodeSetParams(ValidateNode(node), parameters.Pointer), "hipGraphExternalSemaphoresWaitNodeSetParams");

    /// <summary>读取 external semaphore wait node 参数 / Gets external-semaphore wait node parameters.</summary>
    public void GetExternalSemaphoreWaitNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor destination) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExternalSemaphoresWaitNodeGetParams(ValidateNode(node), destination.Pointer), "hipGraphExternalSemaphoresWaitNodeGetParams");

    /// <summary>更新 executable 中的 external semaphore signal node / Updates an external-semaphore signal node in an executable graph.</summary>
    public void UpdateExternalSemaphoreSignalNode(HipGraphExec graphExec, HipAdvancedGraphNode node, HipNativeDescriptor parameters)
    {
        ValidateGraphExecNode(graphExec, node);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExecExternalSemaphoresSignalNodeSetParams(graphExec.DangerousGetHandle(), node.Handle, parameters.Pointer), "hipGraphExecExternalSemaphoresSignalNodeSetParams");
    }

    /// <summary>更新 executable 中的 external semaphore wait node / Updates an external-semaphore wait node in an executable graph.</summary>
    public void UpdateExternalSemaphoreWaitNode(HipGraphExec graphExec, HipAdvancedGraphNode node, HipNativeDescriptor parameters)
    {
        ValidateGraphExecNode(graphExec, node);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExecExternalSemaphoresWaitNodeSetParams(graphExec.DangerousGetHandle(), node.Handle, parameters.Pointer), "hipGraphExecExternalSemaphoresWaitNodeSetParams");
    }

    /// <summary>获取 driver error 名称 / Gets the driver error name.</summary>
    public string GetDriverErrorName(HipError error) => GetDriverError(error, true);
    /// <summary>获取 driver error 描述 / Gets the driver error string.</summary>
    public string GetDriverErrorString(HipError error) => GetDriverError(error, false);

    /// <summary>添加 driver memcpy graph node / Adds a driver memcpy graph node.</summary>
    public HipAdvancedGraphNode AddDriverMemcpyNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, HipNativeDescriptor copyParameters, IntPtr context = default) =>
        AddAdvancedGraphNode(graph, dependencies, copyParameters, false, true, false, context);

    /// <summary>添加 driver memset graph node / Adds a driver memset graph node.</summary>
    public HipAdvancedGraphNode AddDriverMemsetNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, HipNativeDescriptor memsetParameters, IntPtr context = default) =>
        AddAdvancedGraphNode(graph, dependencies, memsetParameters, false, false, true, context);

    /// <summary>添加 driver memory-free graph node / Adds a driver memory-free graph node.</summary>
    public HipAdvancedGraphNode AddDriverMemFreeNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, IntPtr devicePointer)
    {
        if (devicePointer == IntPtr.Zero) throw new ArgumentOutOfRangeException(nameof(devicePointer));
        return AddAdvancedGraphNode(graph, dependencies, default, false, false, false, IntPtr.Zero, devicePointer);
    }

    /// <summary>更新 executable 中的 driver memcpy node / Updates a driver memcpy node in an executable graph.</summary>
    public void UpdateDriverMemcpyNode(HipGraphExec graphExec, HipAdvancedGraphNode node, HipNativeDescriptor copyParameters, IntPtr context = default)
    {
        ValidateGraphExecNode(graphExec, node);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvGraphExecMemcpyNodeSetParams(graphExec.DangerousGetHandle(), node.Handle, copyParameters.Pointer, context), "hipDrvGraphExecMemcpyNodeSetParams");
    }

    /// <summary>更新 executable 中的 driver memset node / Updates a driver memset node in an executable graph.</summary>
    public void UpdateDriverMemsetNode(HipGraphExec graphExec, HipAdvancedGraphNode node, HipNativeDescriptor memsetParameters, IntPtr context = default)
    {
        ValidateGraphExecNode(graphExec, node);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvGraphExecMemsetNodeSetParams(graphExec.DangerousGetHandle(), node.Handle, memsetParameters.Pointer, context), "hipDrvGraphExecMemsetNodeSetParams");
    }

    /// <summary>读取 driver memcpy node 参数 / Gets driver memcpy node parameters.</summary>
    public void GetDriverMemcpyNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor destination) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvGraphMemcpyNodeGetParams(ValidateNode(node), destination.Pointer), "hipDrvGraphMemcpyNodeGetParams");

    /// <summary>设置 driver memcpy node 参数 / Sets driver memcpy node parameters.</summary>
    public void SetDriverMemcpyNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor parameters) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvGraphMemcpyNodeSetParams(ValidateNode(node), parameters.Pointer), "hipDrvGraphMemcpyNodeSetParams");

    /// <summary>通过 driver API 启动 kernel / Launches a kernel through the driver API.</summary>
    public void LaunchDriverKernel(HipNativeDescriptor configuration, IntPtr function, IntPtr parameters = default, IntPtr extra = default)
    {
        if (function == IntPtr.Zero) throw new ArgumentOutOfRangeException(nameof(function));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvLaunchKernelEx(configuration.Pointer, function, parameters, extra), "hipDrvLaunchKernelEx");
    }

    /// <summary>执行 driver 2D 非对齐复制 / Performs an unaligned driver 2D copy.</summary>
    public void CopyDriver2DUnaligned(HipNativeDescriptor parameters) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvMemcpy2DUnaligned(parameters.Pointer), "hipDrvMemcpy2DUnaligned");

    /// <summary>执行 driver 3D 复制 / Performs a driver 3D copy.</summary>
    public void CopyDriver3D(HipNativeDescriptor parameters) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvMemcpy3D(parameters.Pointer), "hipDrvMemcpy3D");

    /// <summary>在 stream 上执行异步 driver 3D 复制 / Performs an asynchronous driver 3D copy on a stream.</summary>
    public void CopyDriver3DAsync(HipNativeDescriptor parameters, HipStream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!ReferenceEquals(stream.NativeApi, _nativeApi)) throw new ArgumentException("Stream belongs to another HIP Runtime client.", nameof(stream));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvMemcpy3DAsync(parameters.Pointer, stream.DangerousGetHandle()), "hipDrvMemcpy3DAsync");
    }

    /// <summary>读取 driver pointer attributes / Gets driver pointer attributes.</summary>
    public void GetDriverPointerAttributes(IReadOnlyList<int> attributes, HipNativeDescriptor values, IntPtr devicePointer)
    {
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));
        if (attributes.Count == 0) throw new ArgumentOutOfRangeException(nameof(attributes));
        if (devicePointer == IntPtr.Zero) throw new ArgumentOutOfRangeException(nameof(devicePointer));
        IntPtr buffer = Marshal.AllocHGlobal(checked(attributes.Count * sizeof(int)));
        try
        {
            for (int index = 0; index < attributes.Count; index++) Marshal.WriteInt32(buffer, index * sizeof(int), attributes[index]);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvPointerGetAttributes(checked((uint)attributes.Count), buffer, values.Pointer, devicePointer), "hipDrvPointerGetAttributes");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal static void WithHandles(HipGraphicsResource[] resources, Action<IntPtr> action)
    {
        IntPtr handles = Marshal.AllocHGlobal(checked(resources.Length * IntPtr.Size));
        try
        {
            for (int index = 0; index < resources.Length; index++) Marshal.WriteIntPtr(handles, index * IntPtr.Size, resources[index].DangerousGetHandle());
            action(handles);
        }
        finally { Marshal.FreeHGlobal(handles); }
    }

    private void EnqueueExternalSemaphores(HipStream stream, IReadOnlyList<HipExternalSemaphore> semaphores, HipNativeDescriptor parameters, bool signal)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (semaphores is null) throw new ArgumentNullException(nameof(semaphores));
        if (semaphores.Count == 0) throw new ArgumentOutOfRangeException(nameof(semaphores));
        if (!ReferenceEquals(stream.NativeApi, _nativeApi)) throw new ArgumentException("Stream belongs to another HIP Runtime client.", nameof(stream));
        IntPtr handles = Marshal.AllocHGlobal(checked(semaphores.Count * IntPtr.Size));
        try
        {
            for (int index = 0; index < semaphores.Count; index++)
            {
                HipExternalSemaphore semaphore = semaphores[index] ?? throw new ArgumentException("Semaphores cannot contain null values.", nameof(semaphores));
                Marshal.WriteIntPtr(handles, index * IntPtr.Size, semaphore.DangerousGetHandle());
            }
            HipError error = signal
                ? _nativeApi.SignalExternalSemaphoresAsync(handles, parameters.Pointer, checked((uint)semaphores.Count), stream.DangerousGetHandle())
                : _nativeApi.WaitExternalSemaphoresAsync(handles, parameters.Pointer, checked((uint)semaphores.Count), stream.DangerousGetHandle());
            HipCall.ThrowIfFailed(_nativeApi, error, signal ? "hipSignalExternalSemaphoresAsync" : "hipWaitExternalSemaphoresAsync");
        }
        finally { Marshal.FreeHGlobal(handles); }
    }

    private void ValidateGraphUserObject(HipGraph graph, HipUserObject userObject, uint count)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (userObject is null) throw new ArgumentNullException(nameof(userObject));
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (!ReferenceEquals(graph.NativeApi, _nativeApi) || !ReferenceEquals(userObject.NativeApi, _nativeApi)) throw new ArgumentException("Graph and user object must belong to this HIP Runtime client.");
    }

    private HipAdvancedGraphNode AddAdvancedGraphNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, HipNativeDescriptor parameters, bool externalSignal, bool driverMemcpy, bool driverMemset, IntPtr context = default, IntPtr devicePointer = default)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (!ReferenceEquals(graph.NativeApi, _nativeApi)) throw new ArgumentException("Graph belongs to another HIP Runtime client.", nameof(graph));
        IntPtr output = AllocateOutputHandle();
        try
        {
            WithNodeHandles(graph, dependencies, (handles, count) =>
            {
                HipError error;
                if (driverMemcpy) error = _nativeApi.DrvGraphAddMemcpyNode(output, graph.DangerousGetHandle(), handles, count, parameters.Pointer, context);
                else if (driverMemset) error = _nativeApi.DrvGraphAddMemsetNode(output, graph.DangerousGetHandle(), handles, count, parameters.Pointer, context);
                else if (devicePointer != IntPtr.Zero) error = _nativeApi.DrvGraphAddMemFreeNode(output, graph.DangerousGetHandle(), handles, count, devicePointer);
                else if (externalSignal) error = _nativeApi.GraphAddExternalSemaphoresSignalNode(output, graph.DangerousGetHandle(), handles, count, parameters.Pointer);
                else error = _nativeApi.GraphAddExternalSemaphoresWaitNode(output, graph.DangerousGetHandle(), handles, count, parameters.Pointer);
                HipCall.ThrowIfFailed(_nativeApi, error, driverMemcpy ? "hipDrvGraphAddMemcpyNode" : driverMemset ? "hipDrvGraphAddMemsetNode" : devicePointer != IntPtr.Zero ? "hipDrvGraphAddMemFreeNode" : externalSignal ? "hipGraphAddExternalSemaphoresSignalNode" : "hipGraphAddExternalSemaphoresWaitNode");
            });
            return new HipAdvancedGraphNode(graph, ReadOutputHandle(output, "graph node creation"));
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    private static void WithNodeHandles(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, Action<IntPtr, UIntPtr> action)
    {
        if (dependencies is null || dependencies.Count == 0) { action(IntPtr.Zero, UIntPtr.Zero); return; }
        IntPtr handles = Marshal.AllocHGlobal(checked(dependencies.Count * IntPtr.Size));
        try
        {
            for (int index = 0; index < dependencies.Count; index++)
            {
                HipAdvancedGraphNode node = dependencies[index] ?? throw new ArgumentException("Dependencies cannot contain null values.", nameof(dependencies));
                if (!ReferenceEquals(node.Graph, graph) || !node.IsValid) throw new ArgumentException("Dependencies must belong to the target graph.", nameof(dependencies));
                Marshal.WriteIntPtr(handles, index * IntPtr.Size, node.Handle);
            }
            action(handles, UIntPtr.Size == 4 ? new UIntPtr(checked((uint)dependencies.Count)) : new UIntPtr((ulong)dependencies.Count));
        }
        finally { Marshal.FreeHGlobal(handles); }
    }

    private IntPtr ValidateNode(HipAdvancedGraphNode node)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (!ReferenceEquals(node.Graph.NativeApi, _nativeApi) || !node.IsValid) throw new ArgumentException("Node belongs to another HIP Runtime client or is no longer valid.", nameof(node));
        return node.Handle;
    }

    private void ValidateGraphExecNode(HipGraphExec graphExec, HipAdvancedGraphNode node)
    {
        if (graphExec is null) throw new ArgumentNullException(nameof(graphExec));
        ValidateNode(node);
        if (!ReferenceEquals(graphExec.NativeApi, _nativeApi)) throw new ArgumentException("Graph executable belongs to another HIP Runtime client.", nameof(graphExec));
        if (graphExec.ExplicitGraph is null || !ReferenceEquals(graphExec.ExplicitGraph, node.Graph)) throw new ArgumentException("Node does not belong to the executable's explicit graph.", nameof(node));
    }

    private string GetDriverError(HipError error, bool name)
    {
        IntPtr output = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(output, IntPtr.Zero);
            HipCall.ThrowIfFailed(_nativeApi, name ? _nativeApi.DrvGetErrorName(error, output) : _nativeApi.DrvGetErrorString(error, output), name ? "hipDrvGetErrorName" : "hipDrvGetErrorString");
            IntPtr text = Marshal.ReadIntPtr(output);
            return text == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(text) ?? string.Empty;
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    private static IntPtr AllocateOutputHandle()
    {
        IntPtr output = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(output, IntPtr.Zero);
        return output;
    }

    private static IntPtr ReadOutputHandle(IntPtr output, string operation)
    {
        IntPtr handle = Marshal.ReadIntPtr(output);
        if (handle == IntPtr.Zero) throw new InvalidOperationException(operation + " succeeded but returned a null handle.");
        return handle;
    }

    private static void InvokeStreamCallback(IntPtr stream, HipError status, IntPtr userData)
    {
        try
        {
            GCHandle handle = GCHandle.FromIntPtr(userData);
            try { ((Action<HipError>)handle.Target!)(status); }
            catch { }
            finally { handle.Free(); }
        }
        catch { }
    }
}

public sealed partial class HipRuntime
{
    private HipAdvancedInterop? _advancedInterop;

    /// <summary>获取外部资源、IPC、callback、profiling 和驱动兼容性的高级托管 interop / Gets advanced managed interop for external resources, IPC, callbacks, profiling, and driver compatibility.</summary>
    public HipAdvancedInterop AdvancedInterop
    {
        get
        {
            ThrowIfDisposed();
            return _advancedInterop ??= new HipAdvancedInterop(this, _nativeApi);
        }
    }
}
