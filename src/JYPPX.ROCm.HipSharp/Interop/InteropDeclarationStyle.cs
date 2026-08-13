namespace JYPPX.ROCm.HipSharp.Interop;

/// <summary>
/// 互操作声明的生成方式 / Generation style used for interop declarations.
/// </summary>
internal enum InteropDeclarationStyle
{
    /// <summary>
    /// 使用传统的 <see cref="System.Runtime.InteropServices.DllImportAttribute" /> / Uses the legacy <see cref="System.Runtime.InteropServices.DllImportAttribute" /> path.
    /// </summary>
    DllImport,

    /// <summary>
    /// 使用源生成的 <c>LibraryImport</c> / Uses the source-generated <c>LibraryImport</c> path.
    /// </summary>
    LibraryImport,
}

/// <summary>
/// 描述当前目标框架选择的互操作生成配置 / Describes the interop generation profile selected for the current target framework.
/// </summary>
internal static class InteropGenerationProfile
{
#if NET7_0_OR_GREATER
    /// <summary>
    /// 当前目标使用 LibraryImport / The current target uses LibraryImport.
    /// </summary>
    internal const InteropDeclarationStyle DeclarationStyle = InteropDeclarationStyle.LibraryImport;
#else
    /// <summary>
    /// 当前目标使用 DllImport / The current target uses DllImport.
    /// </summary>
    internal const InteropDeclarationStyle DeclarationStyle = InteropDeclarationStyle.DllImport;
#endif
}
