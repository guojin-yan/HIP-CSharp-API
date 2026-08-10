using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace JYPPX.HipSharp.Rtc;

/// <summary>
/// 保存一次成功 HIPRTC 编译的代码对象、日志和选项快照 / Stores the code object, log, and option snapshot from a successful HIPRTC compilation.
/// </summary>
public sealed class HipRtcCompilation
{
    private readonly byte[] _codeObject;

    internal HipRtcCompilation(byte[] codeObject, string log, IList<string> options)
    {
        _codeObject = (byte[])codeObject.Clone();
        Log = log;
        Options = new ReadOnlyCollection<string>(new List<string>(options));
        CodeSha256 = ComputeSha256(_codeObject);
    }

    /// <summary>获取 HIPRTC 编译日志 / Gets the HIPRTC compilation log.</summary>
    public string Log { get; }

    /// <summary>获取传给 HIPRTC 的编译选项快照 / Gets a snapshot of compiler options passed to HIPRTC.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>获取代码对象字节数 / Gets the code-object size in bytes.</summary>
    public ulong CodeSize => (ulong)_codeObject.LongLength;

    /// <summary>获取代码对象的 SHA-256（大写十六进制） / Gets the code object's SHA-256 in uppercase hexadecimal.</summary>
    public string CodeSha256 { get; }

    /// <summary>
    /// 返回代码对象副本 / Returns a copy of the code object.
    /// </summary>
    /// <returns>代码对象副本 / A copy of the code object.</returns>
    public byte[] GetCodeObject() => (byte[])_codeObject.Clone();

    private static string ComputeSha256(byte[] value)
    {
        byte[] hash;
#if NET5_0_OR_GREATER
        hash = SHA256.HashData(value);
#else
        using (SHA256 sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(value);
        }
#endif

        var builder = new StringBuilder(hash.Length * 2);
        foreach (byte item in hash)
        {
            builder.Append(item.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
