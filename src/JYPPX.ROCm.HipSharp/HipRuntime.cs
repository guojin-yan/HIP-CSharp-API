using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Peer;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp;

/// <summary>
/// 提供 HIP Runtime 初始化、版本、设备和基础内存操作 / Provides HIP Runtime initialization, version, device, and basic memory operations.
/// </summary>
public sealed class HipRuntime : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _memoryPoolScopeSync = new();
    private readonly Dictionary<int, Stack<HipMemoryPoolCurrentScope>> _memoryPoolScopes = new();
    private int _disposeState;

    /// <summary>
    /// 创建 HIP Runtime 客户端并加载原生库 / Creates a HIP Runtime client and loads the native library.
    /// </summary>
    /// <param name="nativeLibraryPath">可选的绝对原生库路径 / Optional absolute path to the native library.</param>
    /// <exception cref="Loading.HipLibraryLoadException">无法加载 HIP 原生库 / The HIP native library cannot be loaded.</exception>
    /// <exception cref="ArgumentException">显式库路径不是绝对路径 / The explicit library path is not absolute.</exception>
    public HipRuntime(string? nativeLibraryPath = null)
        : this(new PInvokeHipNativeApi(nativeLibraryPath))
    {
    }

    internal HipRuntime(IHipNativeApi nativeApi)
    {
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
    }

    /// <summary>
    /// 初始化 HIP Runtime / Initializes the HIP Runtime.
    /// </summary>
    /// <param name="flags">保留标志，当前应传入零 / Reserved flags; currently pass zero.</param>
    /// <exception cref="HipException">HIP 初始化失败 / HIP initialization fails.</exception>
    public void Initialize(uint flags = 0)
    {
        ThrowIfDisposed();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.Init(flags), "hipInit");
    }

    /// <summary>
    /// 获取 HIP Runtime 与驱动版本 / Gets the HIP Runtime and driver versions.
    /// </summary>
    /// <returns>版本信息 / Version information.</returns>
    /// <exception cref="HipException">HIP 无法返回版本 / HIP cannot return a version.</exception>
    public HipRuntimeVersionInfo GetVersionInfo()
    {
        ThrowIfDisposed();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.RuntimeGetVersion(out int runtimeVersion), "hipRuntimeGetVersion");
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DriverGetVersion(out int driverVersion), "hipDriverGetVersion");
        return new HipRuntimeVersionInfo(new HipVersion(runtimeVersion), new HipVersion(driverVersion));
    }

    /// <summary>
    /// 枚举所有可用 HIP 设备 / Enumerates all available HIP devices.
    /// </summary>
    /// <returns>只读设备列表 / A read-only device list.</returns>
    /// <exception cref="HipException">HIP 无法枚举设备 / HIP cannot enumerate devices.</exception>
    public IReadOnlyList<HipDevice> GetDevices()
    {
        ThrowIfDisposed();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDeviceCount(out int count), "hipGetDeviceCount");
        if (count < 0)
        {
            throw new InvalidOperationException("hipGetDeviceCount returned a negative device count.");
        }

        var devices = new List<HipDevice>(count);
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            devices.Add(GetDevice(ordinal));
        }

        return new ReadOnlyCollection<HipDevice>(devices);
    }

    /// <summary>
    /// 获取指定序号的 HIP 设备 / Gets a HIP device by ordinal.
    /// </summary>
    /// <param name="ordinal">进程内设备序号 / Process-local device ordinal.</param>
    /// <returns>设备对象 / The device object.</returns>
    /// <exception cref="ArgumentOutOfRangeException">设备序号为负数 / The device ordinal is negative.</exception>
    /// <exception cref="HipException">HIP 无法读取设备名称 / HIP cannot read the device name.</exception>
    public HipDevice GetDevice(int ordinal)
    {
        ThrowIfDisposed();
        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetName(ordinal, out string name), "hipDeviceGetName");
        return new HipDevice(_nativeApi, new HipDeviceInfo(ordinal, name));
    }

    /// <summary>
    /// 获取当前 HIP 设备 / Gets the current HIP device.
    /// </summary>
    /// <returns>当前设备 / The current device.</returns>
    /// <exception cref="HipException">HIP 无法返回当前设备 / HIP cannot return the current device.</exception>
    public HipDevice GetCurrentDevice()
    {
        ThrowIfDisposed();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int ordinal), "hipGetDevice");
        return GetDevice(ordinal);
    }

    /// <summary>
    /// 读取一个受 ABI 契约覆盖的设备属性 / Reads a device attribute covered by the ABI contract.
    /// </summary>
    /// <param name="attribute">属性枚举 / Attribute enumeration.</param>
    /// <param name="ordinal">设备序号；负数表示当前设备 / Device ordinal; a negative value means the current device.</param>
    /// <returns>HIP 返回的整数属性值 / Integer value returned by HIP.</returns>
    /// <exception cref="ArgumentOutOfRangeException">设备序号小于 -1 / The ordinal is less than -1.</exception>
    /// <exception cref="HipException">HIP 无法读取属性 / HIP cannot read the attribute.</exception>
    public int GetDeviceAttribute(HipDeviceAttribute attribute, int ordinal = -1)
    {
        ThrowIfDisposed();
        if (ordinal < -1) throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (ordinal < 0) HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out ordinal), "hipGetDevice");
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetAttribute(out int value, attribute, ordinal), "hipDeviceGetAttribute");
        return value;
    }

    /// <summary>创建拥有型 HIP stream / Creates an owning HIP stream.</summary>
    /// <param name="flags">创建标志 / Creation flags.</param>
    /// <returns>新 stream / The new stream.</returns>
    /// <exception cref="HipException">HIP 无法创建 stream / HIP cannot create a stream.</exception>
    public HipStream CreateStream(HipStreamFlags flags = HipStreamFlags.Default)
    {
        ThrowIfDisposed();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int deviceOrdinal), "hipGetDevice");
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamCreateWithFlags(out IntPtr stream, (uint)flags), "hipStreamCreateWithFlags");
        if (stream == IntPtr.Zero) throw new InvalidOperationException("hipStreamCreateWithFlags succeeded but returned a null stream.");
        return new HipStream(_nativeApi, stream, flags, deviceOrdinal);
    }

    /// <summary>创建拥有型 HIP event / Creates an owning HIP event.</summary>
    /// <param name="flags">创建标志 / Creation flags.</param>
    /// <returns>新 event / The new event.</returns>
    /// <exception cref="HipException">HIP 无法创建 event / HIP cannot create an event.</exception>
    public HipEvent CreateEvent(HipEventFlags flags = HipEventFlags.Default)
    {
        ThrowIfDisposed();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.EventCreateWithFlags(out IntPtr handle, (uint)flags), "hipEventCreateWithFlags");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipEventCreateWithFlags succeeded but returned a null event.");
        return new HipEvent(_nativeApi, handle, flags);
    }

    /// <summary>
    /// 在指定 stream 上捕获操作并返回独立 graph owner / Captures operations on a stream and returns an independent graph owner.
    /// </summary>
    public HipGraph CaptureGraph(HipStream stream, Action<HipStream> capture, HipStreamCaptureMode mode = HipStreamCaptureMode.Global)
    {
        ThrowIfDisposed();
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (capture is null) throw new ArgumentNullException(nameof(capture));
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Stream belongs to a different HIP Runtime client.", nameof(stream));
        stream.BeginCapture(mode);
        try
        {
            capture(stream);
            return stream.EndCapture();
        }
        catch
        {
            if (stream.IsCapturing)
            {
                try { stream.EndCapture().Dispose(); }
                catch (HipException) { }
                catch (InvalidOperationException) { }
            }
            throw;
        }
    }

    /// <summary>在当前设备创建可显式构建的 HIP graph / Creates an explicitly buildable HIP graph on the current device.</summary>
    /// <param name="flags">保留 flags；当前必须为零 / Reserved flags; currently must be zero.</param>
    /// <returns>拥有 explicit graph 的对象 / An object owning the explicit graph.</returns>
    public HipGraph CreateGraph(uint flags = 0)
    {
        ThrowIfDisposed();
        if (flags != 0) throw new ArgumentOutOfRangeException(nameof(flags));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int deviceOrdinal), "hipGetDevice");
        HipError error = _nativeApi.GraphCreate(out IntPtr graph, flags);
        if (error != HipError.Success && graph != IntPtr.Zero)
        {
            var partial = new HipGraphHandle(_nativeApi, graph);
            if (partial.ReleaseChecked() == HipError.Success) partial.Dispose();
        }
        HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphCreate");
        if (graph == IntPtr.Zero) throw new InvalidOperationException("hipGraphCreate succeeded but returned a null graph.");
        return new HipGraph(_nativeApi, graph, new HipGraphResources(new List<IDisposable>()), HipGraphKind.Explicit, deviceOrdinal);
    }

    /// <summary>
    /// 在当前设备上分配内存 / Allocates memory on the current device.
    /// </summary>
    /// <param name="byteCount">分配字节数，必须大于零 / Number of bytes to allocate; must be greater than zero.</param>
    /// <returns>拥有分配结果的可释放对象 / A disposable object that owns the allocation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">字节数为零或超过进程地址范围 / The byte count is zero or exceeds the process address range.</exception>
    /// <exception cref="HipException">HIP 无法分配设备内存 / HIP cannot allocate device memory.</exception>
    public HipDeviceMemory Allocate(ulong byteCount)
    {
        ThrowIfDisposed();
        UIntPtr nativeByteCount = HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount));
        if (byteCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        }

        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int deviceOrdinal), "hipGetDevice");
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.Malloc(out IntPtr pointer, nativeByteCount), "hipMalloc");
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("hipMalloc succeeded but returned a null pointer.");
        }

        return new HipDeviceMemory(_nativeApi, pointer, byteCount, deviceOrdinal);
    }

    /// <summary>
    /// 在 stream 上按顺序分配设备内存；owner 必须先于 stream 释放 / Allocates stream-ordered device memory; the owner must be disposed before the stream.
    /// </summary>
    public HipAsyncDeviceMemory AllocateAsync(ulong byteCount, HipStream stream)
    {
        ThrowIfDisposed();
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Stream belongs to a different HIP Runtime client.", nameof(stream));
        if (byteCount == 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        UIntPtr nativeByteCount = HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount));
        IDisposable ownerLease = stream.RegisterOwnedResource();
        bool ownerLeaseTransferred = false;
        try
        {
            IntPtr streamHandle = stream.DangerousGetHandle();
            HipError error = _nativeApi.MallocAsync(out IntPtr pointer, nativeByteCount, streamHandle);
            if (error != HipError.Success && pointer != IntPtr.Zero)
            {
                var partialHandle = new HipAsyncDeviceMemoryHandle(_nativeApi, pointer, streamHandle, ownerLease);
                ownerLeaseTransferred = true;
                if (partialHandle.ReleaseAsyncChecked() == HipError.Success) partialHandle.Dispose();
            }
            HipCall.ThrowIfFailed(_nativeApi, error, "hipMallocAsync");
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("hipMallocAsync succeeded but returned a null pointer.");
            var memory = new HipAsyncDeviceMemory(_nativeApi, pointer, byteCount, stream, ownerLease);
            ownerLeaseTransferred = true;
            return memory;
        }
        catch
        {
            if (!ownerLeaseTransferred) ownerLease.Dispose();
            throw;
        }
    }

    /// <summary>创建仅用于进程内分配且不支持 IPC 导出的 custom memory pool / Creates a custom process-local memory pool that does not support IPC export.</summary>
    /// <param name="options">backing device 和 typed policy / Backing device and typed policy.</param>
    /// <returns>拥有原生 pool 的 owner / An owner of the native pool.</returns>
    public HipMemoryPool CreateMemoryPool(HipMemoryPoolOptions options)
    {
        ThrowIfDisposed();
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (!ReferenceEquals(_nativeApi, options.Device.NativeApi)) throw new ArgumentException("Device belongs to a different HIP Runtime client.", nameof(options));
        UIntPtr maximumSize = HipDeviceMemory.ToUIntPtr(options.MaximumSizeBytes, nameof(options.MaximumSizeBytes));
        var properties = HipMemoryPoolPropertiesNative.ForDevice(options.Device.Ordinal, maximumSize);
        HipError error = _nativeApi.MemPoolCreate(out IntPtr handle, ref properties);
        if (error != HipError.Success && handle != IntPtr.Zero)
        {
            var partial = new HipMemoryPoolHandle(_nativeApi, handle);
            if (partial.ReleaseChecked() == HipError.Success) partial.Dispose();
        }
        HipCall.ThrowIfFailed(_nativeApi, error, "hipMemPoolCreate");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipMemPoolCreate succeeded but returned a null pool handle.");

        var pool = new HipMemoryPool(this, _nativeApi, handle, options.Device.Ordinal, true);
        try
        {
            pool.ReleaseThresholdBytes = options.ReleaseThresholdBytes;
            pool.AllowEventDependencyReuse = options.AllowEventDependencyReuse;
            pool.AllowOpportunisticReuse = options.AllowOpportunisticReuse;
            pool.AllowInternalDependencyReuse = options.AllowInternalDependencyReuse;
            return pool;
        }
        catch
        {
            try { pool.Dispose(); } catch { }
            throw;
        }
    }

    /// <summary>获取设备 default pool 的 borrowed managed view；释放 view 不销毁 Runtime-owned pool / Gets a borrowed managed view of a device's default pool; disposing the view does not destroy the Runtime-owned pool.</summary>
    public HipMemoryPool GetDefaultMemoryPool(HipDevice device)
    {
        ValidateMemoryPoolDevice(device, nameof(device));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetDefaultMemPool(out IntPtr handle, device.Ordinal), "hipDeviceGetDefaultMemPool");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipDeviceGetDefaultMemPool succeeded but returned a null pool handle.");
        return new HipMemoryPool(this, _nativeApi, handle, device.Ordinal, false);
    }

    /// <summary>获取设备 current pool 的 borrowed managed view；释放 view 不销毁 pool / Gets a borrowed managed view of a device's current pool; disposing the view does not destroy the pool.</summary>
    public HipMemoryPool GetCurrentMemoryPool(HipDevice device)
    {
        ValidateMemoryPoolDevice(device, nameof(device));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetMemPool(out IntPtr handle, device.Ordinal), "hipDeviceGetMemPool");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipDeviceGetMemPool succeeded but returned a null pool handle.");
        return new HipMemoryPool(this, _nativeApi, handle, device.Ordinal, false);
    }

    /// <summary>
    /// 分配 CPU/GPU 可见的 managed memory；平台不支持时报告原生错误 / Allocates CPU/GPU-visible managed memory and reports native capability failures.
    /// </summary>
    public HipManagedMemory AllocateManaged(ulong byteCount, HipManagedMemoryFlags flags = HipManagedMemoryFlags.Global)
    {
        ThrowIfDisposed();
        if (byteCount == 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        if (flags != HipManagedMemoryFlags.Global && flags != HipManagedMemoryFlags.Host) throw new ArgumentOutOfRangeException(nameof(flags));
        HipError error = _nativeApi.MallocManaged(out IntPtr pointer, HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount)), (uint)flags);
        if (error != HipError.Success && pointer != IntPtr.Zero)
        {
            var partialHandle = new HipDeviceMemoryHandle(_nativeApi, pointer);
            if (partialHandle.ReleaseChecked() == HipError.Success) partialHandle.Dispose();
        }
        HipCall.ThrowIfFailed(_nativeApi, error, "hipMallocManaged");
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("hipMallocManaged succeeded but returned a null pointer.");
        return new HipManagedMemory(_nativeApi, pointer, byteCount, flags);
    }

    /// <summary>查询显式设备对的 peer capability / Queries peer capability for an explicit device pair.</summary>
    public bool CanAccessPeer(int accessingDevice, int peerDevice)
    {
        ThrowIfDisposed();
        if (accessingDevice < 0) throw new ArgumentOutOfRangeException(nameof(accessingDevice));
        if (peerDevice < 0) throw new ArgumentOutOfRangeException(nameof(peerDevice));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceCanAccessPeer(out int canAccess, accessingDevice, peerDevice), "hipDeviceCanAccessPeer");
        return canAccess != 0;
    }

    /// <summary>
    /// 为当前设备创建显式 peer-access owner；当前设备必须等于 accessingDevice / Creates an explicit peer-access owner; the current device must equal accessingDevice.
    /// </summary>
    public HipPeerAccess EnablePeerAccess(int accessingDevice, int peerDevice)
    {
        ThrowIfDisposed();
        if (accessingDevice < 0) throw new ArgumentOutOfRangeException(nameof(accessingDevice));
        if (peerDevice < 0) throw new ArgumentOutOfRangeException(nameof(peerDevice));
        if (accessingDevice == peerDevice) return new HipPeerAccess(_nativeApi, accessingDevice, peerDevice, false, false, false, false);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int currentDevice), "hipGetDevice");
        if (currentDevice != accessingDevice) throw new InvalidOperationException("The current HIP device does not match accessingDevice. Make the device current explicitly before enabling peer access.");
        if (!CanAccessPeer(accessingDevice, peerDevice)) return new HipPeerAccess(_nativeApi, accessingDevice, peerDevice, false, false, false, false);
        HipError error = _nativeApi.DeviceEnablePeerAccess(peerDevice, 0);
        if (error == HipError.PeerAccessAlreadyEnabled)
            return new HipPeerAccess(_nativeApi, accessingDevice, peerDevice, true, true, false, true);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipDeviceEnablePeerAccess");
        return new HipPeerAccess(_nativeApi, accessingDevice, peerDevice, true, true, true, false);
    }

    /// <summary>分配 typed device memory / Allocates typed device memory.</summary>
    /// <typeparam name="T">非托管元素类型 / Unmanaged element type.</typeparam>
    /// <param name="elementCount">元素数量 / Number of elements.</param>
    /// <returns>typed device memory / Typed device memory.</returns>
    public unsafe HipTypedMemory<T> Allocate<T>(ulong elementCount) where T : unmanaged
    {
        ThrowIfDisposed();
        ulong byteCount;
        try
        {
            byteCount = checked(elementCount * (ulong)sizeof(T));
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), "The typed allocation byte length overflows UInt64.");
        }
        return new HipTypedMemory<T>(Allocate(byteCount), elementCount);
    }

    /// <summary>分配 pinned host memory / Allocates pinned host memory.</summary>
    /// <param name="byteCount">字节数 / Number of bytes.</param>
    /// <returns>拥有型 pinned memory / Owning pinned memory.</returns>
    public HipPinnedMemory AllocatePinned(ulong byteCount)
    {
        ThrowIfDisposed();
        UIntPtr nativeByteCount = HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount));
        if (byteCount == 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.HostMalloc(out IntPtr pointer, nativeByteCount, 0), "hipHostMalloc");
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("hipHostMalloc succeeded but returned a null pointer.");
        return new HipPinnedMemory(_nativeApi, pointer, byteCount);
    }

    /// <summary>
    /// 从内存代码对象加载 HIP module / Loads a HIP module from an in-memory code object.
    /// </summary>
    /// <param name="codeObject">HIPRTC 或兼容工具生成的代码对象 / A code object produced by HIPRTC or a compatible tool.</param>
    /// <returns>拥有原生 module 的对象 / An object that owns the native module.</returns>
    /// <exception cref="ArgumentNullException">代码对象为 null / The code object is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">代码对象为空 / The code object is empty.</exception>
    /// <exception cref="HipException">HIP 无法加载 module / HIP cannot load the module.</exception>
    public HipModule LoadModule(byte[] codeObject)
    {
        ThrowIfDisposed();
        if (codeObject is null)
        {
            throw new ArgumentNullException(nameof(codeObject));
        }

        if (codeObject.Length == 0)
        {
            throw new ArgumentException("A HIP module code object cannot be empty.", nameof(codeObject));
        }

        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int deviceOrdinal), "hipGetDevice");
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ModuleLoadData(codeObject, out IntPtr module), "hipModuleLoadData");
        if (module == IntPtr.Zero)
        {
            throw new InvalidOperationException("hipModuleLoadData succeeded but returned a null module.");
        }

        return new HipModule(_nativeApi, module, deviceOrdinal);
    }

    /// <summary>
    /// 等待当前设备上的工作完成 / Waits for work on the current device to complete.
    /// </summary>
    /// <exception cref="HipException">设备同步失败 / Device synchronization fails.</exception>
    public void Synchronize()
    {
        ThrowIfDisposed();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceSynchronize(), "hipDeviceSynchronize");
    }

    /// <summary>
    /// 查询当前设备的可用与总显存，结果单位为字节 / Queries free and total memory for the current device, in bytes.
    /// </summary>
    /// <returns>不可变显存统计 / Immutable memory statistics.</returns>
    /// <exception cref="ObjectDisposedException">Runtime facade 已释放 / The runtime facade is disposed.</exception>
    /// <exception cref="HipException"><c>hipMemGetInfo</c> 失败或 export 缺失 / <c>hipMemGetInfo</c> fails or its export is unavailable.</exception>
    public HipMemoryInfo GetMemoryInfo()
    {
        ThrowIfDisposed();
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemGetInfo(out UIntPtr freeBytes, out UIntPtr totalBytes), "hipMemGetInfo");
        return new HipMemoryInfo(freeBytes.ToUInt64(), totalBytes.ToUInt64());
    }

    /// <summary>
    /// 在当前设备分配二维 pitched memory；宽高均以 <typeparamref name="T"/> 元素为单位 / Allocates two-dimensional pitched memory on the current device; width and height are measured in <typeparamref name="T"/> elements.
    /// </summary>
    /// <typeparam name="T">非托管元素类型 / Unmanaged element type.</typeparam>
    /// <param name="width">元素宽度，必须大于零 / Width in elements; must be positive.</param>
    /// <param name="height">元素高度，必须大于零 / Height in elements; must be positive.</param>
    /// <returns>独占 allocation 的 owner / Owner that exclusively owns the allocation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">维度为零或 byte/地址尺寸溢出 / A dimension is zero or a byte/address-size calculation overflows.</exception>
    /// <exception cref="ObjectDisposedException">Runtime facade 已释放 / The runtime facade is disposed.</exception>
    /// <exception cref="HipException"><c>hipMallocPitch</c> 失败或 export 缺失 / <c>hipMallocPitch</c> fails or its export is unavailable.</exception>
    public unsafe HipPitchedDeviceMemory<T> Allocate2D<T>(ulong width, ulong height) where T : unmanaged
    {
        ThrowIfDisposed();
        HipMemoryExtent extent = new(width, height);
        ulong widthBytes = CheckedElementBytes<T>(width, nameof(width));
        UIntPtr nativeWidth = HipDeviceMemory.ToUIntPtr(widthBytes, nameof(width));
        UIntPtr nativeHeight = HipDeviceMemory.ToUIntPtr(height, nameof(height));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int deviceOrdinal), "hipGetDevice");

        HipError error = _nativeApi.MallocPitch(out IntPtr pointer, out UIntPtr pitch, nativeWidth, nativeHeight);
        if (error != HipError.Success && pointer != IntPtr.Zero) ReleasePartialAllocation(pointer);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipMallocPitch");
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("hipMallocPitch succeeded but returned a null pointer.");
        ulong pitchBytes = pitch.ToUInt64();
        if (pitchBytes < widthBytes)
        {
            ReleasePartialAllocation(pointer);
            throw new InvalidOperationException("hipMallocPitch returned a row pitch smaller than the requested row width.");
        }

        try
        {
            return new HipPitchedDeviceMemory<T>(_nativeApi, pointer, extent, pitchBytes, widthBytes, height, deviceOrdinal);
        }
        catch
        {
            ReleasePartialAllocation(pointer);
            throw;
        }
    }

    /// <summary>
    /// 在当前设备分配三维 pitched memory；各维度均以 <typeparamref name="T"/> 元素为单位 / Allocates three-dimensional pitched memory on the current device; all dimensions are measured in <typeparamref name="T"/> elements.
    /// </summary>
    /// <typeparam name="T">非托管元素类型 / Unmanaged element type.</typeparam>
    /// <param name="width">元素宽度，必须大于零 / Width in elements; must be positive.</param>
    /// <param name="height">元素高度，必须大于零 / Height in elements; must be positive.</param>
    /// <param name="depth">元素深度，必须大于零 / Depth in elements; must be positive.</param>
    /// <returns>独占 allocation 的 owner / Owner that exclusively owns the allocation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">维度为零或 byte/地址尺寸溢出 / A dimension is zero or a byte/address-size calculation overflows.</exception>
    /// <exception cref="ObjectDisposedException">Runtime facade 已释放 / The runtime facade is disposed.</exception>
    /// <exception cref="HipException"><c>hipMalloc3D</c> 失败或 export 缺失 / <c>hipMalloc3D</c> fails or its export is unavailable.</exception>
    public unsafe HipPitchedDeviceMemory<T> Allocate3D<T>(ulong width, ulong height, ulong depth) where T : unmanaged
    {
        ThrowIfDisposed();
        HipMemoryExtent extent = new(width, height, depth);
        ulong widthBytes = CheckedElementBytes<T>(width, nameof(width));
        HipExtent nativeExtent = new(
            HipDeviceMemory.ToUIntPtr(widthBytes, nameof(width)),
            HipDeviceMemory.ToUIntPtr(height, nameof(height)),
            HipDeviceMemory.ToUIntPtr(depth, nameof(depth)));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int deviceOrdinal), "hipGetDevice");

        HipError error = _nativeApi.Malloc3D(out HipPitchedPtr pitched, nativeExtent);
        if (error != HipError.Success && pitched.Address != IntPtr.Zero) ReleasePartialAllocation(pitched.Address);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipMalloc3D");
        if (pitched.Address == IntPtr.Zero) throw new InvalidOperationException("hipMalloc3D succeeded but returned a null pointer.");
        ulong pitchBytes = pitched.Pitch.ToUInt64();
        ulong xSizeBytes = pitched.XSize.ToUInt64();
        ulong ySize = pitched.YSize.ToUInt64();
        if (pitchBytes < widthBytes || xSizeBytes < widthBytes || ySize < height)
        {
            ReleasePartialAllocation(pitched.Address);
            throw new InvalidOperationException("hipMalloc3D returned a pitched extent smaller than the requested extent.");
        }

        try
        {
            return new HipPitchedDeviceMemory<T>(_nativeApi, pitched.Address, extent, pitchBytes, xSizeBytes, ySize, deviceOrdinal);
        }
        catch
        {
            ReleasePartialAllocation(pitched.Address);
            throw;
        }
    }

    /// <summary>
    /// 释放此轻量 Runtime facade；已返回的独立资源 owner 保持有效 / Disposes this lightweight Runtime facade; independent resource owners already returned remain valid.
    /// </summary>
    public void Dispose()
    {
        lock (_memoryPoolScopeSync)
        {
            foreach (Stack<HipMemoryPoolCurrentScope> scopes in _memoryPoolScopes.Values)
            {
                if (scopes.Count != 0) throw new InvalidOperationException("Dispose all current memory-pool scopes before disposing the runtime client.");
            }
            Interlocked.Exchange(ref _disposeState, 1);
        }
    }

    internal HipMemoryPoolCurrentScope BeginMemoryPoolCurrentScope(HipMemoryPool pool)
    {
        lock (_memoryPoolScopeSync)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(_nativeApi, pool.NativeApi)) throw new ArgumentException("Pool belongs to a different HIP Runtime client.", nameof(pool));
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetMemPool(out IntPtr previous, pool.DeviceOrdinal), "hipDeviceGetMemPool");
            if (previous == IntPtr.Zero) throw new InvalidOperationException("hipDeviceGetMemPool succeeded but returned a null pool handle.");
            pool.AcquireCurrentUse();
            try
            {
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceSetMemPool(pool.DeviceOrdinal, pool.DangerousGetHandle()), "hipDeviceSetMemPool");
                var scope = new HipMemoryPoolCurrentScope(this, pool, previous);
                if (!_memoryPoolScopes.TryGetValue(pool.DeviceOrdinal, out Stack<HipMemoryPoolCurrentScope>? stack))
                {
                    stack = new Stack<HipMemoryPoolCurrentScope>();
                    _memoryPoolScopes.Add(pool.DeviceOrdinal, stack);
                }
                stack.Push(scope);
                return scope;
            }
            catch
            {
                pool.ReleaseCurrentUse();
                throw;
            }
        }
    }

    internal void EndMemoryPoolCurrentScope(HipMemoryPoolCurrentScope scope)
    {
        lock (_memoryPoolScopeSync)
        {
            ThrowIfDisposed();
            if (!_memoryPoolScopes.TryGetValue(scope.Pool.DeviceOrdinal, out Stack<HipMemoryPoolCurrentScope>? stack) || stack.Count == 0 || !ReferenceEquals(stack.Peek(), scope))
                throw new InvalidOperationException("Current memory-pool scopes must be disposed in LIFO order.");
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceSetMemPool(scope.Pool.DeviceOrdinal, scope.PreviousHandle), "hipDeviceSetMemPool");
            stack.Pop();
            scope.Pool.ReleaseCurrentUse();
        }
    }

    internal void ThrowIfDisposedInternal() => ThrowIfDisposed();

    private static unsafe ulong CheckedElementBytes<T>(ulong elementCount, string parameterName) where T : unmanaged
    {
        try
        {
            ulong result = checked(elementCount * (ulong)sizeof(T));
            HipDeviceMemory.ToUIntPtr(result, parameterName);
            return result;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The element byte length overflows UInt64.");
        }
    }

    private void ReleasePartialAllocation(IntPtr pointer)
    {
        var partial = new HipDeviceMemoryHandle(_nativeApi, pointer);
        if (partial.ReleaseChecked() == HipError.Success) partial.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0) throw new ObjectDisposedException(nameof(HipRuntime));
    }

    private void ValidateMemoryPoolDevice(HipDevice device, string parameterName)
    {
        ThrowIfDisposed();
        if (device is null) throw new ArgumentNullException(parameterName);
        if (!ReferenceEquals(_nativeApi, device.NativeApi)) throw new ArgumentException("Device belongs to a different HIP Runtime client.", parameterName);
    }
}
