using System;
using JYPPX.HipSharp.Interop;

namespace JYPPX.HipSharp.Rtc;

/// <summary>
/// 提供 HIPRTC 版本查询和 program 创建 / Provides HIPRTC version queries and program creation.
/// </summary>
public sealed class HipRtc
{
    private readonly IHipRtcNativeApi _nativeApi;

    /// <summary>
    /// 创建 HIPRTC 客户端并加载 HIPRTC 原生库 / Creates a HIPRTC client and loads the HIPRTC native library.
    /// </summary>
    /// <param name="nativeLibraryPath">可选 HIPRTC 原生库绝对路径 / Optional absolute path to the HIPRTC native library.</param>
    /// <exception cref="Loading.HipLibraryLoadException">无法加载 HIPRTC 原生库 / The HIPRTC native library cannot be loaded.</exception>
    /// <exception cref="ArgumentException">显式库路径不是绝对路径 / The explicit library path is not absolute.</exception>
    public HipRtc(string? nativeLibraryPath = null)
        : this(new PInvokeHipRtcNativeApi(nativeLibraryPath))
    {
    }

    internal HipRtc(IHipRtcNativeApi nativeApi)
    {
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
    }

    /// <summary>
    /// 获取已加载 HIPRTC 的版本 / Gets the loaded HIPRTC version.
    /// </summary>
    /// <returns>HIPRTC 版本 / HIPRTC version.</returns>
    /// <exception cref="HipRtcException">HIPRTC 无法返回版本 / HIPRTC cannot return its version.</exception>
    public HipRtcVersion GetVersion()
    {
        HipRtcCall.ThrowIfFailed(_nativeApi, _nativeApi.Version(out int major, out int minor), "hiprtcVersion");
        return new HipRtcVersion(major, minor);
    }

    /// <summary>
    /// 从 HIP C++ 源码创建可编译 program / Creates a compilable program from HIP C++ source.
    /// </summary>
    /// <param name="source">UTF-8 HIP C++ 源码 / UTF-8 HIP C++ source.</param>
    /// <param name="name">用于诊断的源码名称 / Source name used in diagnostics.</param>
    /// <returns>拥有原生 program 的对象 / An object that owns the native program.</returns>
    /// <exception cref="ArgumentNullException">源码或名称为 null / Source or name is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">源码或名称包含 null 字符 / Source or name contains a null character.</exception>
    /// <exception cref="HipRtcException">program 创建失败 / Program creation fails.</exception>
    public HipRtcProgram CreateProgram(string source, string name = "kernel.hip")
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        HipRtcResult result = _nativeApi.CreateProgram(source, name, out IntPtr program);
        HipRtcCall.ThrowIfFailed(_nativeApi, result, "hiprtcCreateProgram");
        if (program == IntPtr.Zero)
        {
            throw new InvalidOperationException("hiprtcCreateProgram succeeded but returned a null program.");
        }

        return new HipRtcProgram(_nativeApi, program);
    }
}
