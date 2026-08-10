using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp;

/// <summary>
/// 提供 HIP Runtime 初始化、版本、设备和基础内存操作 / Provides HIP Runtime initialization, version, device, and basic memory operations.
/// </summary>
public sealed class HipRuntime
{
    private readonly IHipNativeApi _nativeApi;

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
    public void Initialize(uint flags = 0) => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.Init(flags), "hipInit");

    /// <summary>
    /// 获取 HIP Runtime 与驱动版本 / Gets the HIP Runtime and driver versions.
    /// </summary>
    /// <returns>版本信息 / Version information.</returns>
    /// <exception cref="HipException">HIP 无法返回版本 / HIP cannot return a version.</exception>
    public HipRuntimeVersionInfo GetVersionInfo()
    {
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
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamCreateWithFlags(out IntPtr stream, (uint)flags), "hipStreamCreateWithFlags");
        if (stream == IntPtr.Zero) throw new InvalidOperationException("hipStreamCreateWithFlags succeeded but returned a null stream.");
        return new HipStream(_nativeApi, stream, flags);
    }

    /// <summary>创建拥有型 HIP event / Creates an owning HIP event.</summary>
    /// <param name="flags">创建标志 / Creation flags.</param>
    /// <returns>新 event / The new event.</returns>
    /// <exception cref="HipException">HIP 无法创建 event / HIP cannot create an event.</exception>
    public HipEvent CreateEvent(HipEventFlags flags = HipEventFlags.Default)
    {
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.EventCreateWithFlags(out IntPtr handle, (uint)flags), "hipEventCreateWithFlags");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipEventCreateWithFlags succeeded but returned a null event.");
        return new HipEvent(_nativeApi, handle, flags);
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
        UIntPtr nativeByteCount = HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount));
        if (byteCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        }

        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.Malloc(out IntPtr pointer, nativeByteCount), "hipMalloc");
        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("hipMalloc succeeded but returned a null pointer.");
        }

        return new HipDeviceMemory(_nativeApi, pointer, byteCount);
    }

    /// <summary>分配 typed device memory / Allocates typed device memory.</summary>
    /// <typeparam name="T">非托管元素类型 / Unmanaged element type.</typeparam>
    /// <param name="elementCount">元素数量 / Number of elements.</param>
    /// <returns>typed device memory / Typed device memory.</returns>
    public unsafe HipTypedMemory<T> Allocate<T>(ulong elementCount) where T : unmanaged
    {
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
        if (codeObject is null)
        {
            throw new ArgumentNullException(nameof(codeObject));
        }

        if (codeObject.Length == 0)
        {
            throw new ArgumentException("A HIP module code object cannot be empty.", nameof(codeObject));
        }

        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ModuleLoadData(codeObject, out IntPtr module), "hipModuleLoadData");
        if (module == IntPtr.Zero)
        {
            throw new InvalidOperationException("hipModuleLoadData succeeded but returned a null module.");
        }

        return new HipModule(_nativeApi, module);
    }

    /// <summary>
    /// 等待当前设备上的工作完成 / Waits for work on the current device to complete.
    /// </summary>
    /// <exception cref="HipException">设备同步失败 / Device synchronization fails.</exception>
    public void Synchronize() => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceSynchronize(), "hipDeviceSynchronize");
}
