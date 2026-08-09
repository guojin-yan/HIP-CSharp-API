namespace JYPPX.HipSharp.Native.Interop;

internal enum InteropDeclarationStyle
{
    DllImport,
    LibraryImport,
}

internal static class InteropGenerationProfile
{
#if NET7_0_OR_GREATER
    internal const InteropDeclarationStyle DeclarationStyle = InteropDeclarationStyle.LibraryImport;
#else
    internal const InteropDeclarationStyle DeclarationStyle = InteropDeclarationStyle.DllImport;
#endif
}
