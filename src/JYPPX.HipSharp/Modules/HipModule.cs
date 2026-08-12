using System;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Modules;

/// <summary>
/// 拥有从内存代码对象加载的 HIP module / Owns a HIP module loaded from an in-memory code object.
/// </summary>
public sealed class HipModule : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipModuleHandle _handle;
    private readonly object _sync = new();
    private int _asyncReferences;
    private bool _disposeRequested;

    internal HipModule(IHipNativeApi nativeApi, IntPtr module, int deviceOrdinal)
    {
        _nativeApi = nativeApi;
        _handle = new HipModuleHandle(nativeApi, module);
        DeviceOrdinal = deviceOrdinal;
    }

    /// <summary>获取 module 是否已经释放 / Gets whether the module has been released.</summary>
    public bool IsDisposed { get { lock (_sync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid; } }

    /// <summary>
    /// 按名称获取 kernel function / Gets a kernel function by name.
    /// </summary>
    /// <param name="name">UTF-8 kernel 名称 / UTF-8 kernel name.</param>
    /// <returns>保持 module 所有者引用的 kernel / A kernel that retains its module owner.</returns>
    /// <exception cref="ArgumentNullException">名称为 null / The name is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">名称为空或包含 null 字符 / The name is empty or contains a null character.</exception>
    /// <exception cref="ObjectDisposedException">module 已释放 / The module has been released.</exception>
    /// <exception cref="HipException">HIP 无法查找 function / HIP cannot find the function.</exception>
    public HipKernel GetKernel(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (string.IsNullOrWhiteSpace(name) || ContainsNull(name))
        {
            throw new ArgumentException("A kernel name must be non-empty and cannot contain null characters.", nameof(name));
        }

        return Invoke(module =>
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ModuleGetFunction(module, name, out IntPtr function), "hipModuleGetFunction");
            if (function == IntPtr.Zero)
            {
                throw new InvalidOperationException("hipModuleGetFunction succeeded but returned a null function.");
            }

            return new HipKernel(this, function, name);
        });
    }

    /// <summary>
    /// 卸载原生 module；重复调用不会重复卸载 / Unloads the native module; repeated calls do not unload it twice.
    /// </summary>
    /// <exception cref="HipException">HIP 无法卸载 module；此时可重试 / HIP cannot unload the module; disposal can be retried.</exception>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_handle.IsClosed || _handle.IsInvalid) return;
            _disposeRequested = true;
            if (_asyncReferences != 0)
            {
                return;
            }
        }
        ReleaseChecked();
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    internal int DeviceOrdinal { get; }

    internal void AcquireAsyncReference()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            bool addedReference = false;
            _handle.DangerousAddRef(ref addedReference);
            if (!addedReference) throw new ObjectDisposedException(nameof(HipModule));
            _asyncReferences++;
        }
    }

    internal void ReleaseAsyncReference()
    {
        bool releaseChecked;
        lock (_sync)
        {
            if (_asyncReferences > 0)
            {
                _handle.DangerousRelease();
                _asyncReferences--;
            }
            releaseChecked = _disposeRequested && _asyncReferences == 0;
        }

        if (releaseChecked) ReleaseChecked();
    }

    internal T Invoke<T>(Func<IntPtr, T> action)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            T result = action(_handle.DangerousGetHandle());
            GC.KeepAlive(this);
            return result;
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(HipModule));
        }
    }

    private void ReleaseChecked()
    {
        HipError error = _handle.ReleaseChecked();
        if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipModuleUnload");
        _handle.Dispose();
    }

    private static bool ContainsNull(string value)
    {
#if NETCOREAPP3_1_OR_GREATER
        return value.Contains('\0');
#else
        return value.IndexOf('\0') >= 0;
#endif
    }
}
