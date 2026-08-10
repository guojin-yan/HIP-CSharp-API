using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JYPPX.HipSharp.Interop;

/// <summary>
/// 为一次原生调用拥有以 null 结尾的 UTF-8 字符串 / Owns a null-terminated UTF-8 string for one native call.
/// </summary>
internal sealed class Utf8NativeString : IDisposable
{
    internal Utf8NativeString(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (ContainsNull(value))
        {
            throw new ArgumentException("Native strings cannot contain embedded null characters.", parameterName);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
        Pointer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, Pointer, bytes.Length);
    }

    internal IntPtr Pointer { get; private set; }

    public void Dispose()
    {
        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Pointer);
            Pointer = IntPtr.Zero;
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
