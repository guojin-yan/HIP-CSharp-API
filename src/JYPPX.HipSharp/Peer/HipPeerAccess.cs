using System;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Peer;

/// <summary>
/// 表示显式设备对的 peer-access 状态；不使用全局单例隐藏多设备状态 / Represents peer-access state for an explicit device pair without hiding multi-device state in a global singleton.
/// </summary>
public sealed class HipPeerAccess : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly bool _ownsEnable;
    private readonly object _sync = new();
    private bool _isEnabled;
    private bool _disposed;

    internal HipPeerAccess(IHipNativeApi nativeApi, int accessingDevice, int peerDevice, bool supported, bool enabled, bool ownsEnable, bool alreadyEnabled)
    {
        _nativeApi = nativeApi;
        AccessingDevice = accessingDevice;
        PeerDevice = peerDevice;
        IsSupported = supported;
        _isEnabled = enabled;
        _ownsEnable = ownsEnable;
        WasAlreadyEnabled = alreadyEnabled;
    }

    /// <summary>获取发起访问的当前设备 / Gets the current device initiating access.</summary>
    public int AccessingDevice { get; }
    /// <summary>获取 peer 设备 / Gets the peer device.</summary>
    public int PeerDevice { get; }
    /// <summary>获取设备对是否报告 capability / Gets whether the device pair reports capability.</summary>
    public bool IsSupported { get; }
    /// <summary>获取访问是否已启用 / Gets whether access is enabled.</summary>
    public bool IsEnabled { get { lock (_sync) return _isEnabled; } }
    /// <summary>获取访问是否在创建 owner 前已启用 / Gets whether access was already enabled before this owner was created.</summary>
    public bool WasAlreadyEnabled { get; }

    /// <summary>
    /// 在指定 stream 上执行设备对异步复制并保留两个内存 owner / Copies asynchronously for the pair and retains both memory owners.
    /// </summary>
    /// <remarks>AccessingDevice 必须保持为当前设备，且 stream 必须创建于该设备 / AccessingDevice must remain current and the stream must have been created on it.</remarks>
    public void CopyAsync(HipDeviceMemory destination, HipDeviceMemory source, ulong byteCount, HipStream stream)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!IsSupported || !_isEnabled) throw new InvalidOperationException("Peer access is not supported and enabled for this device pair.");
            if (!IsPair(destination.DeviceOrdinal, source.DeviceOrdinal)) throw new ArgumentException("The allocation devices do not match this peer-access pair.");
            if (stream.DeviceOrdinal != AccessingDevice) throw new ArgumentException("The copy stream must have been created on AccessingDevice.", nameof(stream));
            if (byteCount > destination.ByteLength || byteCount > source.ByteLength) throw new ArgumentOutOfRangeException(nameof(byteCount));
            if (!ReferenceEquals(_nativeApi, destination.NativeApi) || !ReferenceEquals(_nativeApi, source.NativeApi) || !ReferenceEquals(_nativeApi, stream.NativeApi))
                throw new ArgumentException("Peer memory and stream must belong to the same HIP Runtime client.");
            EnsureAccessingDeviceIsCurrent("copying peer memory");
            if (byteCount == 0) return;

            bool destinationReference = false;
            bool sourceReference = false;
            bool transferred = false;
            try
            {
                IntPtr destinationPointer = destination.DangerousAcquireHandle(out destinationReference);
                IntPtr sourcePointer = source.DangerousAcquireHandle(out sourceReference);
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemcpyPeerAsync(destinationPointer, destination.DeviceOrdinal, sourcePointer, source.DeviceOrdinal, HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount)), stream.DangerousGetHandle()), "hipMemcpyPeerAsync");
                stream.AddPendingLease(new HipAsyncLease(() =>
                {
                    if (sourceReference)
                    {
                        source.DangerousReleaseHandle();
                        sourceReference = false;
                    }
                    if (destinationReference)
                    {
                        destination.DangerousReleaseHandle();
                        destinationReference = false;
                    }
                }));
                transferred = true;
            }
            finally
            {
                if (!transferred)
                {
                    if (sourceReference) source.DangerousReleaseHandle();
                    if (destinationReference) destination.DangerousReleaseHandle();
                }
            }
        }
    }

    /// <summary>
    /// 禁用由此 owner 首次启用的访问；already-enabled 状态不会被擅自撤销 / Disables access first enabled by this owner; pre-existing access is not revoked.
    /// </summary>
    /// <remarks>若此 owner 负责禁用，AccessingDevice 必须为当前设备 / When this owner performs the disable, AccessingDevice must be current.</remarks>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            if (_ownsEnable && _isEnabled)
            {
                EnsureAccessingDeviceIsCurrent("disabling peer access");
                HipError error = _nativeApi.DeviceDisablePeerAccess(PeerDevice);
                if (error != HipError.Success && error != HipError.PeerAccessNotEnabled) HipCall.ThrowIfFailed(_nativeApi, error, "hipDeviceDisablePeerAccess");
                _isEnabled = false;
            }
            _disposed = true;
        }
    }

    private bool IsPair(int first, int second) =>
        (first == AccessingDevice && second == PeerDevice) || (first == PeerDevice && second == AccessingDevice);

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HipPeerAccess));
    }

    private void EnsureAccessingDeviceIsCurrent(string operation)
    {
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int currentDevice), "hipGetDevice");
        if (currentDevice != AccessingDevice)
        {
            throw new InvalidOperationException("The current HIP device must equal AccessingDevice before " + operation + ".");
        }
    }
}
