using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using JYPPX.ROCm.HipSharp.Interop;

namespace JYPPX.ROCm.HipSharp.Rtc;

/// <summary>
/// 拥有 HIPRTC program 并执行编译 / Owns a HIPRTC program and performs compilation.
/// </summary>
public sealed class HipRtcProgram : IDisposable
{
    private readonly IHipRtcNativeApi _nativeApi;
    private readonly HipRtcProgramHandle _handle;
    private readonly object _sync = new();

    internal HipRtcProgram(IHipRtcNativeApi nativeApi, IntPtr program)
    {
        _nativeApi = nativeApi;
        _handle = new HipRtcProgramHandle(nativeApi, program);
    }

    /// <summary>获取 program 是否已经释放 / Gets whether the program has been released.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>
    /// 使用指定选项编译 program / Compiles the program with the specified options.
    /// </summary>
    /// <param name="options">编译选项；null 表示无选项 / Compiler options; <see langword="null"/> means no options.</param>
    /// <returns>包含代码对象、日志和选项快照的编译结果 / Compilation output containing the code object, log, and option snapshot.</returns>
    /// <exception cref="ArgumentException">选项集合包含 null 元素或 null 字符 / An option is null or contains a null character.</exception>
    /// <exception cref="ObjectDisposedException">program 已释放 / The program has been released.</exception>
    /// <exception cref="HipRtcException">编译或结果读取失败；编译失败异常包含编译日志 / Compilation or result retrieval fails; compilation failures include the compiler log.</exception>
    public HipRtcCompilation Compile(IEnumerable<string>? options = null)
    {
        List<string> optionSnapshot = SnapshotOptions(options);
        lock (_sync)
        {
            ThrowIfDisposed();
            IntPtr program = _handle.DangerousGetHandle();
            HipRtcResult result = _nativeApi.CompileProgram(program, optionSnapshot);
            if (result != HipRtcResult.Success)
            {
                string failedLog = TryReadLog(program);
                HipRtcCall.ThrowIfFailed(_nativeApi, result, "hiprtcCompileProgram", failedLog);
            }

            string log = ReadLog(program);
            byte[] codeObject = ReadCode(program);
            return new HipRtcCompilation(codeObject, log, optionSnapshot);
        }
    }

    /// <summary>
    /// 销毁原生 HIPRTC program；重复调用不会重复销毁 / Destroys the native HIPRTC program; repeated calls do not destroy it twice.
    /// </summary>
    /// <exception cref="HipRtcException">HIPRTC 无法销毁 program；此时可重试 / HIPRTC cannot destroy the program; disposal can be retried.</exception>
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

            HipRtcCall.ThrowIfFailed(_nativeApi, result, "hiprtcDestroyProgram");
        }
    }

    private static List<string> SnapshotOptions(IEnumerable<string>? options)
    {
        var snapshot = new List<string>();
        if (options is null)
        {
            return snapshot;
        }

        foreach (string option in options)
        {
            if (option is null)
            {
                throw new ArgumentException("Compiler options cannot contain null elements.", nameof(options));
            }

            if (ContainsNull(option))
            {
                throw new ArgumentException("Compiler options cannot contain null characters.", nameof(options));
            }

            snapshot.Add(option);
        }

        return snapshot;
    }

    private string ReadLog(IntPtr program)
    {
        HipRtcCall.ThrowIfFailed(_nativeApi, _nativeApi.GetProgramLogSize(program, out UIntPtr nativeSize), "hiprtcGetProgramLogSize");
        int size = ToManagedSize(nativeSize, "HIPRTC compilation log");
        if (size == 0)
        {
            return string.Empty;
        }

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.WriteByte(buffer, 0);
            HipRtcCall.ThrowIfFailed(_nativeApi, _nativeApi.GetProgramLog(program, buffer), "hiprtcGetProgramLog");
            return ReadUtf8(buffer, size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private string TryReadLog(IntPtr program)
    {
        try
        {
            return ReadLog(program);
        }
        catch (Exception exception)
        {
            return "[Unable to retrieve HIPRTC compilation log: " + exception.Message + "]";
        }
    }

    private byte[] ReadCode(IntPtr program)
    {
        HipRtcCall.ThrowIfFailed(_nativeApi, _nativeApi.GetCodeSize(program, out UIntPtr nativeSize), "hiprtcGetCodeSize");
        int size = ToManagedSize(nativeSize, "HIPRTC code object");
        if (size == 0)
        {
            throw new InvalidOperationException("hiprtcGetCodeSize succeeded but returned an empty code object.");
        }

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            HipRtcCall.ThrowIfFailed(_nativeApi, _nativeApi.GetCode(program, buffer), "hiprtcGetCode");
            var code = new byte[size];
            Marshal.Copy(buffer, code, 0, size);
            return code;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int ToManagedSize(UIntPtr nativeSize, string valueName)
    {
        ulong size = nativeSize.ToUInt64();
        if (size > int.MaxValue)
        {
            throw new InvalidOperationException(valueName + " is larger than the maximum managed buffer size.");
        }

        return (int)size;
    }

    private static string ReadUtf8(IntPtr buffer, int size)
    {
        var bytes = new byte[size];
        Marshal.Copy(buffer, bytes, 0, size);
        int length = size;
        while (length > 0 && bytes[length - 1] == 0)
        {
            length--;
        }

        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    private static bool ContainsNull(string value)
    {
#if NETCOREAPP3_1_OR_GREATER
        return value.Contains('\0');
#else
        return value.IndexOf('\0') >= 0;
#endif
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(HipRtcProgram));
        }
    }
}
