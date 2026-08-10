using System;
using System.IO;

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 描述成功加载的原生库及其用户态闭包身份 / Describes a successfully loaded native library and its user-mode closure identity.
/// </summary>
internal sealed class HipNativeLibraryLoadResult
{
    internal HipNativeLibraryLoadResult(IntPtr handle, HipLibraryCandidate candidate)
    {
        Handle = handle;
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        ClosureIdentity = GetClosureIdentity(candidate);
    }

    internal IntPtr Handle { get; }

    internal HipLibraryCandidate Candidate { get; }

    internal string ClosureIdentity { get; }

    private static string GetClosureIdentity(HipLibraryCandidate candidate)
    {
        if (Path.IsPathRooted(candidate.Value))
        {
            return "directory:" + (Path.GetDirectoryName(Path.GetFullPath(candidate.Value)) ?? string.Empty);
        }

        return "search:" + candidate.Source;
    }
}
