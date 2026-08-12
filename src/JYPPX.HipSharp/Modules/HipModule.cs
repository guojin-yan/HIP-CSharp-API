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
        ValidateName(name, "kernel");

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
    /// 按名称获取 module-owned 全局符号的 borrowed byte view / Gets a borrowed byte view of a module-owned global symbol by name.
    /// </summary>
    /// <param name="name">UTF-8 global、device 或 constant symbol 名称 / UTF-8 global, device, or constant symbol name.</param>
    /// <returns>随 module 失效且绝不能释放底层指针的 byte view / A byte view invalidated with the module that must never free its underlying pointer.</returns>
    /// <exception cref="ArgumentNullException">名称为 <see langword="null"/> / The name is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">名称为空、全空白或包含 null 字符 / The name is empty, whitespace-only, or contains a null character.</exception>
    /// <exception cref="InvalidOperationException">HIP 成功但返回无效 pointer 或 byte extent / HIP succeeds but returns an invalid pointer or byte extent.</exception>
    /// <exception cref="ObjectDisposedException">module 已释放 / The module has been released.</exception>
    /// <exception cref="HipException">symbol 查询失败，包括 optional export 不可用 / Symbol lookup fails, including an unavailable optional export.</exception>
    public HipModuleGlobal GetGlobal(string name)
    {
        ValidateName(name, "symbol");
        return Invoke(module => GetGlobalCore(module, name));
    }

    /// <summary>
    /// 按名称获取以非托管元素计数的 module-owned global view / Gets an unmanaged-element view of a module-owned global by name.
    /// </summary>
    /// <typeparam name="T">symbol 元素的非托管类型 / Unmanaged symbol element type.</typeparam>
    /// <param name="name">UTF-8 symbol 名称 / UTF-8 symbol name.</param>
    /// <returns>以 <typeparamref name="T"/> 元素为单位的 borrowed view / A borrowed view measured in <typeparamref name="T"/> elements.</returns>
    /// <exception cref="ArgumentNullException">名称为 <see langword="null"/> / The name is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">名称无效，或 symbol 字节数不能被 <typeparamref name="T"/> 大小整除 / The name is invalid, or the symbol byte length is not divisible by the size of <typeparamref name="T"/>.</exception>
    /// <exception cref="InvalidOperationException">HIP 返回无效 symbol 范围 / HIP returns an invalid symbol range.</exception>
    /// <exception cref="ObjectDisposedException">module 已释放 / The module has been released.</exception>
    /// <exception cref="HipException">symbol 查询失败，包括 optional export 不可用 / Symbol lookup fails, including an unavailable optional export.</exception>
    public unsafe HipModuleGlobal<T> GetGlobal<T>(string name) where T : unmanaged
    {
        ValidateName(name, "symbol");
        return Invoke(module =>
        {
            HipModuleGlobal global = GetGlobalCore(module, name);
            ulong elementSize = (ulong)sizeof(T);
            if (global.ByteLength % elementSize != 0)
            {
                throw new ArgumentException("The symbol byte length is not divisible by the requested element size.", nameof(name));
            }
            return new HipModuleGlobal<T>(global, global.ByteLength / elementSize);
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

    private HipModuleGlobal GetGlobalCore(IntPtr module, string name)
    {
        HipCall.ThrowIfFailed(
            _nativeApi,
            _nativeApi.ModuleGetGlobal(module, name, out IntPtr pointer, out UIntPtr byteCount),
            "hipModuleGetGlobal");
        ulong length = byteCount.ToUInt64();
        HipModuleGlobal.ValidateNativeRange(pointer, length);
        return new HipModuleGlobal(this, pointer, length, name);
    }

    private static void ValidateName(string name, string kind)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(name) || ContainsNull(name))
        {
            throw new ArgumentException("A " + kind + " name must be non-empty and cannot contain null characters.", nameof(name));
        }
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
