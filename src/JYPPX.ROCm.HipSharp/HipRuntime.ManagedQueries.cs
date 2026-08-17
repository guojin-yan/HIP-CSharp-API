using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp;

/// <summary>提供类型化 Runtime 查询 / Provides typed Runtime queries.</summary>
public sealed partial class HipRuntime
{
    /// <summary>获取该值 / Gets a device by its PCI bus identifier.</summary>
    public unsafe HipDevice GetDeviceByPciBusId(string pciBusId)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(pciBusId)) throw new ArgumentException("A PCI bus identifier is required.", nameof(pciBusId));
        using (var nativePciBusId = new Utf8NativeString(pciBusId, nameof(pciBusId)))
        {
            int ordinal = -1;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetByPCIBusId((IntPtr)(&ordinal), nativePciBusId.Pointer), "hipDeviceGetByPCIBusId");
            if (ordinal < 0) throw new InvalidOperationException("hipDeviceGetByPCIBusId succeeded but returned a negative ordinal.");
            return GetDevice(ordinal);
        }
    }

    /// <summary>获取该值 / Gets the current-device cache configuration.</summary>
    public unsafe HipDeviceCacheConfig GetDeviceCacheConfig()
    {
        ThrowIfDisposed();
        int value = 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetCacheConfig((IntPtr)(&value)), "hipDeviceGetCacheConfig");
        return (HipDeviceCacheConfig)value;
    }

    /// <summary>获取该值 / Gets a current-device graph-memory attribute represented by its native numeric identifier.</summary>
    public unsafe ulong GetDeviceGraphMemoryAttribute(int deviceOrdinal, int nativeAttribute)
    {
        ThrowIfDisposed();
        if (deviceOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(deviceOrdinal));
        ulong value = 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetGraphMemAttribute(deviceOrdinal, nativeAttribute, (IntPtr)(&value)), "hipDeviceGetGraphMemAttribute");
        return value;
    }

    /// <summary>获取该值 / Gets a current-device limit represented by its native numeric identifier.</summary>
    public unsafe ulong GetDeviceLimit(int nativeLimit)
    {
        ThrowIfDisposed();
        UIntPtr value = UIntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetLimit((IntPtr)(&value), nativeLimit), "hipDeviceGetLimit");
        return value.ToUInt64();
    }

    /// <summary>获取该值 / Gets a P2P attribute represented by its native numeric identifier.</summary>
    public unsafe int GetP2PAttribute(int nativeAttribute, int sourceDeviceOrdinal, int destinationDeviceOrdinal)
    {
        ThrowIfDisposed();
        if (sourceDeviceOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(sourceDeviceOrdinal));
        if (destinationDeviceOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(destinationDeviceOrdinal));
        int value = 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetP2PAttribute((IntPtr)(&value), nativeAttribute, sourceDeviceOrdinal, destinationDeviceOrdinal), "hipDeviceGetP2PAttribute");
        return value;
    }

    /// <summary>获取该值 / Gets the current-device shared-memory bank configuration.</summary>
    public unsafe HipSharedMemoryConfig GetDeviceSharedMemoryConfig()
    {
        ThrowIfDisposed();
        int value = 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetSharedMemConfig((IntPtr)(&value)), "hipDeviceGetSharedMemConfig");
        return (HipSharedMemoryConfig)value;
    }

    /// <summary>获取该值 / Gets the supported stream-priority range.</summary>
    public unsafe HipStreamPriorityRange GetStreamPriorityRange()
    {
        ThrowIfDisposed();
        int least = 0;
        int greatest = 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetStreamPriorityRange((IntPtr)(&least), (IntPtr)(&greatest)), "hipDeviceGetStreamPriorityRange");
        return new HipStreamPriorityRange(least, greatest);
    }

    /// <summary>获取该值 / Gets a device symbol address. The supplied symbol pointer remains borrowed.</summary>
    public unsafe IntPtr GetSymbolAddress(IntPtr symbol)
    {
        ThrowIfDisposed();
        if (symbol == IntPtr.Zero) throw new ArgumentException("A non-null native symbol pointer is required.", nameof(symbol));
        IntPtr pointer = IntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetSymbolAddress((IntPtr)(&pointer), symbol), "hipGetSymbolAddress");
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("hipGetSymbolAddress succeeded but returned a null pointer.");
        return pointer;
    }

    /// <summary>获取该值 / Gets a device symbol size. The supplied symbol pointer remains borrowed.</summary>
    public unsafe ulong GetSymbolSize(IntPtr symbol)
    {
        ThrowIfDisposed();
        if (symbol == IntPtr.Zero) throw new ArgumentException("A non-null native symbol pointer is required.", nameof(symbol));
        UIntPtr size = UIntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetSymbolSize((IntPtr)(&size), symbol), "hipGetSymbolSize");
        return size.ToUInt64();
    }

    /// <summary>获取该值 / Gets a native pointer attribute into a caller-selected unmanaged value type.</summary>
    public unsafe T GetPointerAttribute<T>(IntPtr nativeAddress, int nativeAttribute) where T : unmanaged
    {
        ThrowIfDisposed();
        if (nativeAddress == IntPtr.Zero) throw new ArgumentException("A non-null native pointer is required.", nameof(nativeAddress));
        T value = default;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.PointerGetAttribute((IntPtr)(&value), nativeAttribute, nativeAddress), "hipPointerGetAttribute");
        return value;
    }

    /// <summary>获取该值 / Gets the native pointer-attribute structure for a non-null pointer.</summary>
    public unsafe HipPointerAttributes GetPointerAttributes(IntPtr nativeAddress)
    {
        ThrowIfDisposed();
        if (nativeAddress == IntPtr.Zero) throw new ArgumentException("A non-null native pointer is required.", nameof(nativeAddress));
        HipPointerAttributes value = default;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.PointerGetAttributes((IntPtr)(&value), nativeAddress), "hipPointerGetAttributes");
        return value;
    }

    /// <summary>设置该值 / Sets a native pointer attribute from a caller-selected unmanaged value type.</summary>
    public unsafe void SetPointerAttribute<T>(IntPtr nativeAddress, int nativeAttribute, T value) where T : unmanaged
    {
        ThrowIfDisposed();
        if (nativeAddress == IntPtr.Zero) throw new ArgumentException("A non-null native pointer is required.", nameof(nativeAddress));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.PointerSetAttribute((IntPtr)(&value), nativeAttribute, nativeAddress), "hipPointerSetAttribute");
    }
}
