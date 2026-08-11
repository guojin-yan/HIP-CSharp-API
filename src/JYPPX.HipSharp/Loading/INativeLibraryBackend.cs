using System;

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 抽象操作系统原生库加载器以便进行确定性测试 / Abstracts the operating-system native loader for deterministic tests.
/// </summary>
internal interface INativeLibraryBackend
{
    public bool TryLoad(string candidate, out IntPtr handle, out string detail);
    public bool TryGetExport(IntPtr handle, string entryPoint, out IntPtr address, out string detail);
    public void Free(IntPtr handle);
}
