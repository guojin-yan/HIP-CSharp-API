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
    private readonly HipInteropResource _resource;
    internal HipExternalMemory(IHipNativeApi nativeApi, IntPtr handle)
    {
        _nativeApi = nativeApi;
        _resource = new HipInteropResource(handle, value => HipCall.ThrowIfFailed(nativeApi, nativeApi.DestroyExternalMemory(value), "hipDestroyExternalMemory"));
    }
    /// <summary>指示是否已请求释放 / Indicates whether disposal has been requested.</summary>
    public bool IsDisposed => _resource.IsDisposed;
    /// <summary>获取借用 handle / Gets the borrowed handle.</summary>
    public IntPtr DangerousGetHandle() => _resource.GetHandle();
    /// <summary>映射 buffer 并保活父导入 / Maps a buffer while retaining the parent import.</summary>
    public HipExternalMemoryBuffer MapBuffer(HipNativeDescriptor descriptor)
    {
        HipInteropResource.Borrow? parent = null;
        IntPtr output = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            parent = _resource.Acquire();
            Marshal.WriteIntPtr(output, IntPtr.Zero);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ExternalMemoryGetMappedBuffer(output, parent.Handle, descriptor.Pointer), "hipExternalMemoryGetMappedBuffer");
            IntPtr pointer = Marshal.ReadIntPtr(output);
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("hipExternalMemoryGetMappedBuffer succeeded but returned a null pointer.");
            var result = new HipExternalMemoryBuffer(_nativeApi, pointer, parent);
            parent = null;
            return result;
        }
        finally { Marshal.FreeHGlobal(output); parent?.Dispose(); }
    }
    /// <summary>映射 mipmap 并保活父导入 / Maps a mipmap while retaining the parent import.</summary>
    public HipExternalMipmappedArray MapMipmappedArray(HipNativeDescriptor descriptor)
    {
        HipInteropResource.Borrow? parent = null;
        IntPtr output = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            parent = _resource.Acquire();
            Marshal.WriteIntPtr(output, IntPtr.Zero);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ExternalMemoryGetMappedMipmappedArray(output, parent.Handle, descriptor.Pointer), "hipExternalMemoryGetMappedMipmappedArray");
            IntPtr handle = Marshal.ReadIntPtr(output);
            if (handle == IntPtr.Zero) throw new InvalidOperationException("hipExternalMemoryGetMappedMipmappedArray succeeded but returned a null handle.");
            var result = new HipExternalMipmappedArray(_nativeApi, handle, parent);
            parent = null;
            return result;
        }
        finally { Marshal.FreeHGlobal(output); parent?.Dispose(); }
    }
    /// <summary>映射释放后销毁导入；失败可重试 / Destroys after mappings are released; failures can be retried.</summary>
    public void Dispose() => _resource.Dispose();
}

/// <summary>管理外部映射 buffer / Owns an externally mapped buffer.</summary>
public sealed class HipExternalMemoryBuffer : IDisposable
{
    private readonly HipInteropResource _resource;
    internal HipExternalMemoryBuffer(IHipNativeApi nativeApi, IntPtr pointer, IDisposable parent) =>
        _resource = new HipInteropResource(pointer, value => HipCall.ThrowIfFailed(nativeApi, nativeApi.Free(value), "hipFree(external-memory-buffer)"), parent);
    /// <summary>指示是否已请求释放 / Indicates whether disposal has been requested.</summary>
    public bool IsDisposed => _resource.IsDisposed;
    /// <summary>获取借用 pointer / Gets the borrowed pointer.</summary>
    public IntPtr DangerousGetHandle() => _resource.GetHandle();
    /// <summary>释放映射再释放父租约 / Releases the mapping before its parent lease.</summary>
    public void Dispose() => _resource.Dispose();
}

/// <summary>管理外部映射 mipmap / Owns an externally mapped mipmap.</summary>
public sealed class HipExternalMipmappedArray : IDisposable
{
    private readonly HipInteropResource _resource;
    internal HipExternalMipmappedArray(IHipNativeApi nativeApi, IntPtr handle, IDisposable parent) =>
        _resource = new HipInteropResource(handle, value => HipCall.ThrowIfFailed(nativeApi, nativeApi.FreeMipmappedArray(value), "hipFreeMipmappedArray(external-memory)"), parent);
    /// <summary>指示是否已请求释放 / Indicates whether disposal has been requested.</summary>
    public bool IsDisposed => _resource.IsDisposed;
    /// <summary>获取借用 handle / Gets the borrowed handle.</summary>
    public IntPtr DangerousGetHandle() => _resource.GetHandle();
    /// <summary>释放映射再释放父租约 / Releases the mapping before its parent lease.</summary>
    public void Dispose() => _resource.Dispose();
}

/// <summary>管理外部 semaphore 导入 / Owns an external-semaphore import.</summary>
public sealed class HipExternalSemaphore : IDisposable
{
    private readonly HipInteropResource _resource;
    internal HipExternalSemaphore(IHipNativeApi nativeApi, IntPtr handle)
    {
        NativeApi = nativeApi;
        _resource = new HipInteropResource(handle, value => HipCall.ThrowIfFailed(nativeApi, nativeApi.DestroyExternalSemaphore(value), "hipDestroyExternalSemaphore"));
    }
    internal IHipNativeApi NativeApi { get; }
    internal HipInteropResource.Borrow Acquire() => _resource.Acquire();
    /// <summary>指示是否已请求释放 / Indicates whether disposal has been requested.</summary>
    public bool IsDisposed => _resource.IsDisposed;
    /// <summary>获取借用 handle / Gets the borrowed handle.</summary>
    public IntPtr DangerousGetHandle() => _resource.GetHandle();
    /// <summary>异步借用完成后销毁，失败可重试 / Destroys after asynchronous borrows complete; failures can be retried.</summary>
    public void Dispose() => _resource.Dispose();
}

/// <summary>管理注册的 graphics resource / Owns a registered graphics resource.</summary>
public sealed class HipGraphicsResource : IDisposable
{
    private readonly HipInteropResource _resource;
    internal HipGraphicsResource(IHipNativeApi nativeApi, IntPtr handle)
    {
        NativeApi = nativeApi;
        _resource = new HipInteropResource(handle, value => HipCall.ThrowIfFailed(nativeApi, nativeApi.GraphicsUnregisterResource(value), "hipGraphicsUnregisterResource"));
    }
    internal IHipNativeApi NativeApi { get; }
    internal HipInteropResource.Borrow Acquire() => _resource.Acquire();
    /// <summary>指示是否已请求释放 / Indicates whether disposal has been requested.</summary>
    public bool IsDisposed => _resource.IsDisposed;
    /// <summary>获取借用 handle / Gets the borrowed handle.</summary>
    public IntPtr DangerousGetHandle() => _resource.GetHandle();
    /// <summary>取消映射完成后注销 / Unregisters after unmapping completes.</summary>
    public void Dispose() => _resource.Dispose();
}

/// <summary>管理 stream 上的 graphics mapping / Owns a graphics mapping on a stream.</summary>
public sealed class HipGraphicsMapping : IDisposable
{
    private readonly object _sync = new();
    private readonly IHipNativeApi _nativeApi;
    private readonly HipGraphicsResource[] _resources;
    private readonly HipInteropResource.Borrow[] _borrows;
    private readonly HipStream _stream;
    private readonly IDisposable _streamOwner;
    private readonly IDisposable _leases;
    private bool _disposed;
    internal HipGraphicsMapping(IHipNativeApi nativeApi, HipGraphicsResource[] resources, HipInteropResource.Borrow[] borrows, HipStream stream, IDisposable streamOwner, IDisposable leases)
    { _nativeApi = nativeApi; _resources = resources; _borrows = borrows; _stream = stream; _streamOwner = streamOwner; _leases = leases; }
    /// <summary>获取映射 pointer 与长度 / Gets the mapped pointer and length.</summary>
    public HipMappedGraphicsPointer GetMappedPointer(HipGraphicsResource resource)
    {
        lock (_sync)
        {
            IntPtr handle = ValidateResource(resource);
            IntPtr pointer = Marshal.AllocHGlobal(IntPtr.Size);
            IntPtr size = Marshal.AllocHGlobal(UIntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(pointer, IntPtr.Zero);
                if (UIntPtr.Size == 4) Marshal.WriteInt32(size, 0); else Marshal.WriteInt64(size, 0);
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsResourceGetMappedPointer(pointer, size, handle), "hipGraphicsResourceGetMappedPointer");
                return new HipMappedGraphicsPointer(Marshal.ReadIntPtr(pointer), UIntPtr.Size == 4 ? unchecked((uint)Marshal.ReadInt32(size)) : unchecked((ulong)Marshal.ReadInt64(size)));
            }
            finally { Marshal.FreeHGlobal(size); Marshal.FreeHGlobal(pointer); }
        }
    }
    /// <summary>获取借用 array handle / Gets a borrowed array handle.</summary>
    public IntPtr GetMappedArray(HipGraphicsResource resource, uint arrayIndex, uint mipLevel)
    {
        lock (_sync)
        {
            IntPtr handle = ValidateResource(resource);
            IntPtr output = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(output, IntPtr.Zero);
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsSubResourceGetMappedArray(output, handle, arrayIndex, mipLevel), "hipGraphicsSubResourceGetMappedArray");
                return Marshal.ReadIntPtr(output);
            }
            finally { Marshal.FreeHGlobal(output); }
        }
    }
    /// <summary>入队取消映射；完成后释放资源租约 / Enqueues unmapping and releases resource leases after completion.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _stream.EnqueuePending(handle =>
            {
                HipAdvancedInterop.WithBorrowHandles(_borrows, handles => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsUnmapResources(_resources.Length, handles, handle), "hipGraphicsUnmapResources"));
                return _leases;
            }, leases => leases.Dispose());
            _disposed = true;
            _streamOwner.Dispose();
        }
    }
    private IntPtr ValidateResource(HipGraphicsResource resource)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HipGraphicsMapping));
        if (resource is null) throw new ArgumentNullException(nameof(resource));
        int index = Array.IndexOf(_resources, resource);
        if (index < 0) throw new ArgumentException("Resource is not part of this mapping.", nameof(resource));
        return _borrows[index].Handle;
    }
}

/// <summary>映射 graphics resource 的借用 pointer / A borrowed pointer from a mapped graphics resource.</summary>
public readonly struct HipMappedGraphicsPointer
{
    internal HipMappedGraphicsPointer(IntPtr pointer, ulong byteLength) { Pointer = pointer; ByteLength = byteLength; }
    /// <summary>获取 pointer / Gets the pointer.</summary>
    public IntPtr Pointer { get; }
    /// <summary>获取字节长度 / Gets the byte length.</summary>
    public ulong ByteLength { get; }
}

/// <summary>管理 IPC memory pointer / Owns an IPC memory pointer.</summary>
public sealed class HipIpcMemory : IDisposable
{
    private readonly HipInteropResource _resource;
    internal HipIpcMemory(IHipNativeApi nativeApi, IntPtr pointer) =>
        _resource = new HipInteropResource(pointer, value => HipCall.ThrowIfFailed(nativeApi, nativeApi.IpcCloseMemHandle(value), "hipIpcCloseMemHandle"));
    /// <summary>指示是否已请求释放 / Indicates whether disposal has been requested.</summary>
    public bool IsDisposed => _resource.IsDisposed;
    /// <summary>获取借用 pointer / Gets the borrowed pointer.</summary>
    public IntPtr DangerousGetHandle() => _resource.GetHandle();
    /// <summary>关闭 IPC memory，失败可重试 / Closes IPC memory; failures can be retried.</summary>
    public void Dispose() => _resource.Dispose();
}

/// <summary>管理 HIP user object 的全部本地引用 / Owns all local references to a HIP user object.</summary>
public sealed class HipUserObject : IDisposable
{
    private readonly object _sync = new();
    private readonly IHipNativeApi _nativeApi;
    private IntPtr _handle;
    private uint _references;
    private bool _disposeRequested;
    internal HipUserObject(IHipNativeApi nativeApi, IntPtr handle, uint references) { _nativeApi = nativeApi; _handle = handle; _references = references; }
    /// <summary>指示是否已请求释放 / Indicates whether disposal has been requested.</summary>
    public bool IsDisposed { get { lock (_sync) return _disposeRequested; } }
    internal IHipNativeApi NativeApi => _nativeApi;
    internal void WithHandle(Action<IntPtr> operation)
    {
        lock (_sync)
        {
            if (_disposeRequested) throw new ObjectDisposedException(nameof(HipUserObject));
            operation(_handle);
        }
    }
    /// <summary>保留额外本地引用，Dispose 会全部释放 / Retains additional local references, all released by Dispose.</summary>
    public void Retain(uint count = 1)
    {
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        WithHandle(handle =>
        {
            uint total = checked(_references + count);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.UserObjectRetain(handle, count), "hipUserObjectRetain");
            _references = total;
        });
    }
    /// <summary>释放全部本地引用，失败可重试 / Releases all local references; failures can be retried.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            _disposeRequested = true;
            if (_handle == IntPtr.Zero) return;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.UserObjectRelease(_handle, _references), "hipUserObjectRelease");
            _handle = IntPtr.Zero;
            _references = 0;
        }
    }
}

/// <summary>管理 HIP profiler 会话 / Owns a HIP profiler session.</summary>
public sealed class HipProfilerSession : IDisposable
{
    private readonly object _sync = new();
    private readonly IHipNativeApi _nativeApi;
    private bool _disposed;
    internal HipProfilerSession(IHipNativeApi nativeApi) => _nativeApi = nativeApi;
    /// <summary>指示 profiling 是否已停止 / Indicates whether profiling has stopped.</summary>
    public bool IsDisposed { get { lock (_sync) return _disposed; } }
    /// <summary>停止 profiling，失败可重试 / Stops profiling; failures can be retried.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ProfilerStop(), "hipProfilerStop");
            _disposed = true;
        }
    }
}
/// <summary>高级 graph interop API 返回的借用 node 标识 / Borrowed node identity returned by advanced graph interop APIs.</summary>
public sealed class HipAdvancedGraphNode
{
    internal HipAdvancedGraphNode(HipGraphNode node) => Node = node;
    internal HipGraphNode Node { get; }
    /// <summary>获取所属 graph / Gets the owning graph.</summary>
    public HipGraph Graph => Node.Graph;
    /// <summary>指示 node 是否仍有效 / Indicates whether the node is still valid.</summary>
    public bool IsValid => Node.IsValid;
    internal IntPtr Handle => Node.Handle;
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
        _runtime.ThrowIfDisposedInternal();
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
        _runtime.ThrowIfDisposedInternal();
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
        _runtime.ThrowIfDisposedInternal();
        if (resources is null) throw new ArgumentNullException(nameof(resources));
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (resources.Count == 0) throw new ArgumentOutOfRangeException(nameof(resources));
        if (!ReferenceEquals(stream.NativeApi, _nativeApi)) throw new ArgumentException("Stream belongs to another HIP Runtime client.", nameof(stream));
        HipGraphicsResource[] copy = new HipGraphicsResource[resources.Count];
        var borrows = new HipInteropResource.Borrow[resources.Count];
        IDisposable? streamOwner = null;
        bool transferred = false;
        try
        {
            for (int index = 0; index < copy.Length; index++)
            {
                HipGraphicsResource resource = resources[index] ?? throw new ArgumentException("Resources cannot contain null values.", nameof(resources));
                if (!ReferenceEquals(resource.NativeApi, _nativeApi)) throw new ArgumentException("Resource belongs to another HIP Runtime client.", nameof(resources));
                copy[index] = resource;
                if (Array.IndexOf(copy, resource, 0, index) >= 0) throw new ArgumentException("Resources cannot contain duplicates.", nameof(resources));
                borrows[index] = resource.Acquire();
            }
            streamOwner = stream.RegisterOwnedResource();
            var leases = new HipAsyncLease(() => DisposeBorrows(borrows));
            var mapping = new HipGraphicsMapping(_nativeApi, copy, borrows, stream, streamOwner, leases);
            stream.ExecuteInterop(streamHandle => WithBorrowHandles(borrows, handles => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphicsMapResources(copy.Length, handles, streamHandle), "hipGraphicsMapResources")));
            transferred = true;
            return mapping;
        }
        finally
        {
            if (!transferred) { DisposeBorrows(borrows); streamOwner?.Dispose(); }
        }
    }

    /// <summary>获取 device memory 的 IPC handle / Gets an IPC handle for device memory.</summary>
    public HipIpcMemHandle GetIpcMemoryHandle(HipDeviceMemory memory)
    {
        _runtime.ThrowIfDisposedInternal();
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
        _runtime.ThrowIfDisposedInternal();
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
        _runtime.ThrowIfDisposedInternal();
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
        _runtime.ThrowIfDisposedInternal();
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
        _runtime.ThrowIfDisposedInternal();
        if (initialReferenceCount == 0) throw new ArgumentOutOfRangeException(nameof(initialReferenceCount));
        IntPtr output = AllocateOutputHandle();
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.UserObjectCreate(output, value, destroyCallback.Pointer, initialReferenceCount, flags), "hipUserObjectCreate");
            return new HipUserObject(_nativeApi, ReadOutputHandle(output, "hipUserObjectCreate"), initialReferenceCount);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    /// <summary>在 graph 中保留 user object / Retains a user object in a graph.</summary>
    public void RetainUserObject(HipGraph graph, HipUserObject userObject, uint count = 1, uint flags = 0)
    {
        ValidateGraphUserObject(graph, userObject, count);
        if (flags != 0) throw new ArgumentOutOfRangeException(nameof(flags), "Ownership transfer is available through the low-level API; managed owners retain their local references.");
        graph.MutateInterop(graphHandle => userObject.WithHandle(handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphRetainUserObject(graphHandle, handle, count, flags), "hipGraphRetainUserObject")));
    }

    /// <summary>在 graph 中释放 user object 引用 / Releases user-object references from a graph.</summary>
    public void ReleaseUserObject(HipGraph graph, HipUserObject userObject, uint count = 1)
    {
        ValidateGraphUserObject(graph, userObject, count);
        graph.MutateInterop(graphHandle => userObject.WithHandle(handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphReleaseUserObject(graphHandle, handle, count), "hipGraphReleaseUserObject")));
    }

    /// <summary>添加 external semaphore signal graph node / Adds an external-semaphore signal graph node.</summary>
    public HipAdvancedGraphNode AddExternalSemaphoreSignalNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, HipNativeDescriptor parameters) =>
        AddAdvancedGraphNode(graph, dependencies, parameters, true, false, false);

    /// <summary>添加 external semaphore wait graph node / Adds an external-semaphore wait graph node.</summary>
    public HipAdvancedGraphNode AddExternalSemaphoreWaitNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, HipNativeDescriptor parameters) =>
        AddAdvancedGraphNode(graph, dependencies, parameters, false, false, false);

    /// <summary>设置 external semaphore signal node 参数 / Sets external-semaphore signal node parameters.</summary>
    public void SetExternalSemaphoreSignalNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor parameters) =>
        AccessNode(node, HipGraphNodeType.ExternalSemaphoreSignal, true, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExternalSemaphoresSignalNodeSetParams(handle, parameters.Pointer), "hipGraphExternalSemaphoresSignalNodeSetParams"));

    /// <summary>读取 external semaphore signal node 参数 / Gets external-semaphore signal node parameters.</summary>
    public void GetExternalSemaphoreSignalNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor destination) =>
        AccessNode(node, HipGraphNodeType.ExternalSemaphoreSignal, false, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExternalSemaphoresSignalNodeGetParams(handle, destination.Pointer), "hipGraphExternalSemaphoresSignalNodeGetParams"));

    /// <summary>设置 external semaphore wait node 参数 / Sets external-semaphore wait node parameters.</summary>
    public void SetExternalSemaphoreWaitNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor parameters) =>
        AccessNode(node, HipGraphNodeType.ExternalSemaphoreWait, true, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExternalSemaphoresWaitNodeSetParams(handle, parameters.Pointer), "hipGraphExternalSemaphoresWaitNodeSetParams"));

    /// <summary>读取 external semaphore wait node 参数 / Gets external-semaphore wait node parameters.</summary>
    public void GetExternalSemaphoreWaitNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor destination) =>
        AccessNode(node, HipGraphNodeType.ExternalSemaphoreWait, false, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExternalSemaphoresWaitNodeGetParams(handle, destination.Pointer), "hipGraphExternalSemaphoresWaitNodeGetParams"));

    /// <summary>更新 executable 中的 external semaphore signal node / Updates an external-semaphore signal node in an executable graph.</summary>
    public void UpdateExternalSemaphoreSignalNode(HipGraphExec graphExec, HipAdvancedGraphNode node, HipNativeDescriptor parameters)
    {
        UpdateNode(graphExec, node, HipGraphNodeType.ExternalSemaphoreSignal, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExecExternalSemaphoresSignalNodeSetParams(handle, node.Handle, parameters.Pointer), "hipGraphExecExternalSemaphoresSignalNodeSetParams"));
    }

    /// <summary>更新 executable 中的 external semaphore wait node / Updates an external-semaphore wait node in an executable graph.</summary>
    public void UpdateExternalSemaphoreWaitNode(HipGraphExec graphExec, HipAdvancedGraphNode node, HipNativeDescriptor parameters)
    {
        UpdateNode(graphExec, node, HipGraphNodeType.ExternalSemaphoreWait, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExecExternalSemaphoresWaitNodeSetParams(handle, node.Handle, parameters.Pointer), "hipGraphExecExternalSemaphoresWaitNodeSetParams"));
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
        UpdateNode(graphExec, node, HipGraphNodeType.MemoryCopy, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvGraphExecMemcpyNodeSetParams(handle, node.Handle, copyParameters.Pointer, context), "hipDrvGraphExecMemcpyNodeSetParams"));
    }

    /// <summary>更新 executable 中的 driver memset node / Updates a driver memset node in an executable graph.</summary>
    public void UpdateDriverMemsetNode(HipGraphExec graphExec, HipAdvancedGraphNode node, HipNativeDescriptor memsetParameters, IntPtr context = default)
    {
        UpdateNode(graphExec, node, HipGraphNodeType.MemorySet, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvGraphExecMemsetNodeSetParams(handle, node.Handle, memsetParameters.Pointer, context), "hipDrvGraphExecMemsetNodeSetParams"));
    }

    /// <summary>读取 driver memcpy node 参数 / Gets driver memcpy node parameters.</summary>
    public void GetDriverMemcpyNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor destination) =>
        AccessNode(node, HipGraphNodeType.MemoryCopy, false, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvGraphMemcpyNodeGetParams(handle, destination.Pointer), "hipDrvGraphMemcpyNodeGetParams"));

    /// <summary>设置 driver memcpy node 参数 / Sets driver memcpy node parameters.</summary>
    public void SetDriverMemcpyNodeParameters(HipAdvancedGraphNode node, HipNativeDescriptor parameters) =>
        AccessNode(node, HipGraphNodeType.MemoryCopy, true, handle => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvGraphMemcpyNodeSetParams(handle, parameters.Pointer), "hipDrvGraphMemcpyNodeSetParams"));

    /// <summary>通过 driver API 启动 kernel / Launches a kernel through the driver API.</summary>
    public void LaunchDriverKernel(HipNativeDescriptor configuration, IntPtr function, IntPtr parameters = default, IntPtr extra = default)
    {
        _runtime.ThrowIfDisposedInternal();
        if (function == IntPtr.Zero) throw new ArgumentOutOfRangeException(nameof(function));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvLaunchKernelEx(configuration.Pointer, function, parameters, extra), "hipDrvLaunchKernelEx");
    }

    /// <summary>执行 driver 2D 非对齐复制 / Performs an unaligned driver 2D copy.</summary>
    public void CopyDriver2DUnaligned(HipNativeDescriptor parameters)
    {
        _runtime.ThrowIfDisposedInternal();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvMemcpy2DUnaligned(parameters.Pointer), "hipDrvMemcpy2DUnaligned");
    }

    /// <summary>执行 driver 3D 复制 / Performs a driver 3D copy.</summary>
    public void CopyDriver3D(HipNativeDescriptor parameters)
    {
        _runtime.ThrowIfDisposedInternal();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvMemcpy3D(parameters.Pointer), "hipDrvMemcpy3D");
    }

    /// <summary>在 stream 上执行异步 driver 3D 复制 / Performs an asynchronous driver 3D copy on a stream.</summary>
    public void CopyDriver3DAsync(HipNativeDescriptor parameters, HipStream stream)
    {
        _runtime.ThrowIfDisposedInternal();
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!ReferenceEquals(stream.NativeApi, _nativeApi)) throw new ArgumentException("Stream belongs to another HIP Runtime client.", nameof(stream));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DrvMemcpy3DAsync(parameters.Pointer, stream.DangerousGetHandle()), "hipDrvMemcpy3DAsync");
    }

    /// <summary>读取 driver pointer attributes / Gets driver pointer attributes.</summary>
    public void GetDriverPointerAttributes(IReadOnlyList<int> attributes, HipNativeDescriptor values, IntPtr devicePointer)
    {
        _runtime.ThrowIfDisposedInternal();
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

    internal static void WithBorrowHandles(HipInteropResource.Borrow[] borrows, Action<IntPtr> action)
    {
        IntPtr handles = Marshal.AllocHGlobal(checked(borrows.Length * IntPtr.Size));
        try
        {
            for (int index = 0; index < borrows.Length; index++) Marshal.WriteIntPtr(handles, index * IntPtr.Size, borrows[index].Handle);
            action(handles);
        }
        finally { Marshal.FreeHGlobal(handles); }
    }

    private void EnqueueExternalSemaphores(HipStream stream, IReadOnlyList<HipExternalSemaphore> semaphores, HipNativeDescriptor parameters, bool signal)
    {
        _runtime.ThrowIfDisposedInternal();
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (semaphores is null) throw new ArgumentNullException(nameof(semaphores));
        if (semaphores.Count == 0) throw new ArgumentOutOfRangeException(nameof(semaphores));
        if (!ReferenceEquals(stream.NativeApi, _nativeApi)) throw new ArgumentException("Stream belongs to another HIP Runtime client.", nameof(stream));
        var borrows = new HipInteropResource.Borrow[semaphores.Count];
        bool transferred = false;
        try
        {
            for (int index = 0; index < semaphores.Count; index++)
            {
                HipExternalSemaphore semaphore = semaphores[index] ?? throw new ArgumentException("Semaphores cannot contain null values.", nameof(semaphores));
                if (!ReferenceEquals(semaphore.NativeApi, _nativeApi)) throw new ArgumentException("Semaphore belongs to another HIP Runtime client.", nameof(semaphores));
                borrows[index] = semaphore.Acquire();
            }
            stream.EnqueuePending(streamHandle =>
            {
                WithBorrowHandles(borrows, handles =>
                {
                    HipError error = signal
                        ? _nativeApi.SignalExternalSemaphoresAsync(handles, parameters.Pointer, checked((uint)borrows.Length), streamHandle)
                        : _nativeApi.WaitExternalSemaphoresAsync(handles, parameters.Pointer, checked((uint)borrows.Length), streamHandle);
                    HipCall.ThrowIfFailed(_nativeApi, error, signal ? "hipSignalExternalSemaphoresAsync" : "hipWaitExternalSemaphoresAsync");
                });
                return borrows;
            }, DisposeBorrows);
            transferred = true;
        }
        finally { if (!transferred) DisposeBorrows(borrows); }
    }

    private static void DisposeBorrows(HipInteropResource.Borrow[] borrows)
    {
        for (int index = borrows.Length - 1; index >= 0; index--) borrows[index]?.Dispose();
    }

    private void ValidateGraphUserObject(HipGraph graph, HipUserObject userObject, uint count)
    {
        _runtime.ThrowIfDisposedInternal();
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (userObject is null) throw new ArgumentNullException(nameof(userObject));
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (!ReferenceEquals(graph.NativeApi, _nativeApi) || !ReferenceEquals(userObject.NativeApi, _nativeApi)) throw new ArgumentException("Graph and user object must belong to this HIP Runtime client.");
    }

    private HipAdvancedGraphNode AddAdvancedGraphNode(HipGraph graph, IReadOnlyList<HipAdvancedGraphNode>? dependencies, HipNativeDescriptor parameters, bool externalSignal, bool driverMemcpy, bool driverMemset, IntPtr context = default, IntPtr devicePointer = default)
    {
        _runtime.ThrowIfDisposedInternal();
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (!ReferenceEquals(graph.NativeApi, _nativeApi)) throw new ArgumentException("Graph belongs to another HIP Runtime client.", nameof(graph));
        var nodes = new List<HipGraphNode>(dependencies?.Count ?? 0);
        if (dependencies is not null)
        {
            foreach (HipAdvancedGraphNode? dependency in dependencies)
                nodes.Add((dependency ?? throw new ArgumentException("Dependencies cannot contain null values.", nameof(dependencies))).Node);
        }
        HipGraphNodeType type = driverMemcpy ? HipGraphNodeType.MemoryCopy : driverMemset ? HipGraphNodeType.MemorySet : devicePointer != IntPtr.Zero ? HipGraphNodeType.MemoryFree : externalSignal ? HipGraphNodeType.ExternalSemaphoreSignal : HipGraphNodeType.ExternalSemaphoreWait;
        string operation = driverMemcpy ? "hipDrvGraphAddMemcpyNode" : driverMemset ? "hipDrvGraphAddMemsetNode" : devicePointer != IntPtr.Zero ? "hipDrvGraphAddMemFreeNode" : externalSignal ? "hipGraphAddExternalSemaphoresSignalNode" : "hipGraphAddExternalSemaphoresWaitNode";
        IntPtr output = AllocateOutputHandle();
        try
        {
            HipGraphNode node = graph.AddInteropNode(nodes, type, (graphHandle, handles, count) =>
            {
                if (driverMemcpy) return _nativeApi.DrvGraphAddMemcpyNode(output, graphHandle, handles, count, parameters.Pointer, context);
                if (driverMemset) return _nativeApi.DrvGraphAddMemsetNode(output, graphHandle, handles, count, parameters.Pointer, context);
                if (devicePointer != IntPtr.Zero) return _nativeApi.DrvGraphAddMemFreeNode(output, graphHandle, handles, count, devicePointer);
                if (externalSignal) return _nativeApi.GraphAddExternalSemaphoresSignalNode(output, graphHandle, handles, count, parameters.Pointer);
                return _nativeApi.GraphAddExternalSemaphoresWaitNode(output, graphHandle, handles, count, parameters.Pointer);
            }, output, operation);
            return new HipAdvancedGraphNode(node);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    private void ValidateNode(HipAdvancedGraphNode node)
    {
        _runtime.ThrowIfDisposedInternal();
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (!ReferenceEquals(node.Graph.NativeApi, _nativeApi)) throw new ArgumentException("Node belongs to another HIP Runtime client.", nameof(node));
    }

    private void AccessNode(HipAdvancedGraphNode node, HipGraphNodeType type, bool mutate, Action<IntPtr> operation)
    {
        ValidateNode(node);
        node.Graph.AccessInteropNode(node.Node, type, mutate, operation);
    }

    private void UpdateNode(HipGraphExec graphExec, HipAdvancedGraphNode node, HipGraphNodeType type, Action<IntPtr> operation)
    {
        if (graphExec is null) throw new ArgumentNullException(nameof(graphExec));
        ValidateNode(node);
        if (!ReferenceEquals(graphExec.NativeApi, _nativeApi)) throw new ArgumentException("Graph executable belongs to another HIP Runtime client.", nameof(graphExec));
        graphExec.UpdateInteropNode(node.Node, type, operation);
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
