using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JYPPX.ROCm.HipSharp.Interop;

/// <summary>
/// 为一次原生调用拥有 UTF-8 字符串指针数组 / Owns an array of UTF-8 string pointers for one native call.
/// </summary>
internal sealed class Utf8NativeStringArray : IDisposable
{
    private readonly List<Utf8NativeString> _strings = new();

    internal Utf8NativeStringArray(IReadOnlyList<string> values, string parameterName)
    {
        if (values is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        try
        {
            if (values.Count == 0)
            {
                return;
            }

            Pointer = Marshal.AllocHGlobal(checked(values.Count * IntPtr.Size));
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] is null)
                {
                    throw new ArgumentException("Native string arrays cannot contain null elements.", parameterName);
                }

                var value = new Utf8NativeString(values[index], parameterName);
                _strings.Add(value);
                Marshal.WriteIntPtr(Pointer, index * IntPtr.Size, value.Pointer);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal IntPtr Pointer { get; private set; }

    public void Dispose()
    {
        foreach (Utf8NativeString value in _strings)
        {
            value.Dispose();
        }

        _strings.Clear();
        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Pointer);
            Pointer = IntPtr.Zero;
        }
    }
}
