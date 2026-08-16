using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;

namespace JYPPX.ROCm.HipSharp.Rtc;

/// <summary>
/// 拥有 HIPRTC link state、AddData 输入副本和最终 code object 复制边界 / Owns a HIPRTC link state, AddData input copies, and the final code-object copy boundary.
/// </summary>
public sealed class HipRtcLinker : IDisposable
{
    private readonly IHipRtcNativeApi _nativeApi;
    private readonly HipRtcLinkStateHandle _handle;
    private readonly object _sync = new();
    private bool _completed;

    internal HipRtcLinker(IHipRtcNativeApi nativeApi, IntPtr linkState)
    {
        _nativeApi = nativeApi;
        _handle = new HipRtcLinkStateHandle(nativeApi, linkState);
    }

    /// <summary>获取 link state 是否已经释放 / Gets whether the link state has been released.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>获取此 linker 是否已经成功完成 / Gets whether this linker has completed successfully.</summary>
    public bool IsCompleted { get { lock (_sync) return _completed; } }

    /// <summary>
    /// 添加由 HIPRTC linker 读取的文件 / Adds a file for the HIPRTC linker to read.
    /// </summary>
    /// <param name="inputType">AMD linker 输入种类 / AMD linker input kind.</param>
    /// <param name="filePath">UTF-8 文件路径 / UTF-8 file path.</param>
    /// <exception cref="ArgumentException">路径为空、全空白或包含 null 字符 / The path is empty, whitespace-only, or contains a null character.</exception>
    /// <exception cref="ArgumentOutOfRangeException">输入种类不是受支持的 AMD 类型 / The input kind is not a supported AMD kind.</exception>
    /// <exception cref="InvalidOperationException">linker 已完成 / The linker has completed.</exception>
    /// <exception cref="ObjectDisposedException">linker 已释放 / The linker has been released.</exception>
    /// <exception cref="HipRtcException">HIPRTC 无法添加文件 / HIPRTC cannot add the file.</exception>
    public void AddFile(HipRtcJitInputType inputType, string filePath)
    {
        ValidateInputType(inputType);
        ValidateRequiredString(filePath, nameof(filePath), "file path");
        lock (_sync)
        {
            ThrowIfUnavailable();
            HipRtcCall.ThrowIfFailed(
                _nativeApi,
                _nativeApi.LinkAddFile(_handle.DangerousGetHandle(), inputType, filePath),
                "hiprtcLinkAddFile");
            GC.KeepAlive(this);
        }
    }

    /// <summary>
    /// 复制并添加一个内存输入；副本保留到 linker 被释放 / Copies and adds an in-memory input; the copy is retained until the linker is released.
    /// </summary>
    /// <param name="inputType">AMD linker 输入种类 / AMD linker input kind.</param>
    /// <param name="image">非空输入 bytes；调用方随后可修改原数组 / Non-empty input bytes; the caller may modify the original array afterward.</param>
    /// <param name="name">可选 UTF-8 诊断名称 / Optional UTF-8 diagnostic name.</param>
    /// <exception cref="ArgumentNullException">image 为 null / <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">image 为空，或 name 为空、全空白或包含 null 字符 / The image is empty, or the name is empty, whitespace-only, or contains a null character.</exception>
    /// <exception cref="ArgumentOutOfRangeException">输入种类不是受支持的 AMD 类型 / The input kind is not a supported AMD kind.</exception>
    /// <exception cref="InvalidOperationException">linker 已完成 / The linker has completed.</exception>
    /// <exception cref="ObjectDisposedException">linker 已释放 / The linker has been released.</exception>
    /// <exception cref="HipRtcException">HIPRTC 无法添加数据 / HIPRTC cannot add the data.</exception>
    public void AddData(HipRtcJitInputType inputType, byte[] image, string? name = null)
    {
        ValidateInputType(inputType);
        if (image is null) throw new ArgumentNullException(nameof(image));
        if (image.Length == 0) throw new ArgumentException("A linker input image cannot be empty.", nameof(image));
        if (name is not null) ValidateRequiredString(name, nameof(name), "linker input name");

        lock (_sync)
        {
            ThrowIfUnavailable();
            IntPtr buffer = Marshal.AllocHGlobal(checked(image.Length + 1));
            bool tracked = false;
            try
            {
                Marshal.Copy(image, 0, buffer, image.Length);
                Marshal.WriteByte(buffer, image.Length, 0);
                HipRtcCall.ThrowIfFailed(
                    _nativeApi,
                    _nativeApi.LinkAddData(_handle.DangerousGetHandle(), inputType, buffer, new UIntPtr((uint)image.Length), name),
                    "hiprtcLinkAddData");
                _handle.TrackInputBuffer(buffer);
                tracked = true;
                GC.KeepAlive(this);
            }
            finally
            {
                if (!tracked) Marshal.FreeHGlobal(buffer);
            }
        }
    }

    /// <summary>
    /// 完成链接并立即复制由 link state 拥有的 code object / Completes linking and immediately copies the code object owned by the link state.
    /// </summary>
    /// <returns>可在 linker 释放后继续使用的独立 code object / An independent code object usable after the linker is released.</returns>
    /// <exception cref="InvalidOperationException">linker 已完成，或 HIPRTC 返回空/过大输出 / The linker has completed, or HIPRTC returns empty/oversized output.</exception>
    /// <exception cref="ObjectDisposedException">linker 已释放 / The linker has been released.</exception>
    /// <exception cref="HipRtcException">HIPRTC 链接失败 / HIPRTC linking fails.</exception>
    public byte[] Complete()
    {
        lock (_sync)
        {
            ThrowIfUnavailable();
            HipRtcCall.ThrowIfFailed(
                _nativeApi,
                _nativeApi.LinkComplete(_handle.DangerousGetHandle(), out IntPtr codeObject, out UIntPtr nativeSize),
                "hiprtcLinkComplete");
            int size = ToManagedSize(nativeSize);
            if (codeObject == IntPtr.Zero || size == 0)
            {
                throw new InvalidOperationException("hiprtcLinkComplete succeeded but returned an empty code object.");
            }

            var result = new byte[size];
            Marshal.Copy(codeObject, result, 0, size);
            _completed = true;
            GC.KeepAlive(this);
            return result;
        }
    }

    /// <summary>
    /// 销毁原生 link state 并释放所有输入副本；失败时可重试 / Destroys the native link state and releases all input copies; failures can be retried.
    /// </summary>
    /// <exception cref="HipRtcException">HIPRTC 无法销毁 link state；输入副本仍保留供重试 / HIPRTC cannot destroy the link state; input copies remain retained for retry.</exception>
    public void Dispose()
    {
        lock (_sync)
        {
            HipRtcResult result = _handle.ReleaseChecked();
            if (result == HipRtcResult.Success)
            {
                _handle.Dispose();
                return;
            }

            HipRtcCall.ThrowIfFailed(_nativeApi, result, "hiprtcLinkDestroy");
        }
    }

    private static int ToManagedSize(UIntPtr nativeSize)
    {
        ulong size = nativeSize.ToUInt64();
        if (size > int.MaxValue)
        {
            throw new InvalidOperationException("HIPRTC linked code object is larger than the maximum managed buffer size.");
        }

        return (int)size;
    }

    private static void ValidateInputType(HipRtcJitInputType inputType)
    {
        if (inputType != HipRtcJitInputType.LlvmBitcode &&
            inputType != HipRtcJitInputType.LlvmBundledBitcode &&
            inputType != HipRtcJitInputType.LlvmArchivesOfBundledBitcode &&
            inputType != HipRtcJitInputType.SpirV)
        {
            throw new ArgumentOutOfRangeException(nameof(inputType), "Only AMD HIPRTC linker input kinds are supported.");
        }
    }

    private static void ValidateRequiredString(string value, string parameterName, string description)
    {
        if (value is null) throw new ArgumentNullException(parameterName);
        if (string.IsNullOrWhiteSpace(value) || ContainsNull(value))
        {
            throw new ArgumentException("A " + description + " must be non-empty and cannot contain null characters.", parameterName);
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

    private void ThrowIfUnavailable()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipRtcLinker));
        if (_completed) throw new InvalidOperationException("The HIPRTC linker has already completed.");
    }
}
