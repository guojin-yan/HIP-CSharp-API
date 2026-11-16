using System;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    private static void LegacyTextureParameters(bool moduleReference, int variant)
    {
        IntPtr module = 0, texture = 0;
        try
        {
            if (moduleReference)
            {
                byte[] bytes = LoadFatbinary();
                fixed (byte* image = bytes) Check(Raw.ModuleLoadFatBinary((IntPtr)(&module), (IntPtr)image));
                using var name = new Utf8("ValidationTexture");
                if (!Capability(Raw.ModuleGetTexRef((IntPtr)(&texture), module, name.Pointer))) return;
                Require(texture != 0, "Module texture parameter reference.");
            }
            else texture = LoadHelperPointer("RegisteredTexture");
            if (!Capability(Raw.TexRefSetFormat(texture, 1, 1))) return;
            Check(Raw.TexRefSetFlags(texture, 3));
            if (variant == 0)
            {
                float* color = stackalloc float[4] { 0.125f, 0.25f, 0.5f, 1f };
                float* actual = stackalloc float[4] { -99f, -99f, -99f, -99f };
                Check(Raw.TexRefSetBorderColor(texture, (IntPtr)color));
                Check(Raw.TexRefGetBorderColor((IntPtr)actual, texture));
                for (int i = 0; i < 4; i++)
                    Require(actual[i] == color[i], $"Legacy border color channel {i}: expected {color[i]}, actual {actual[i]}.");
            }
            else if (variant == 1)
            {
                Check(Raw.TexRefSetMipmapFilterMode(texture, 0));
                int actual = -99;
                // Installed headers document only InvalidValue/NotSupported for these deprecated getters.
                // This is exact negative evidence, not a successful getter round trip.
                HipError result = Raw.TexRefGetMipmapFilterMode((IntPtr)(&actual), texture);
                if (result == HipError.NotSupported) Capability(result, "Raw.TexRefGetMipmapFilterMode((IntPtr)(&actual), texture)");
                else Check(result, HipError.InvalidValue, "Raw.TexRefGetMipmapFilterMode((IntPtr)(&actual), texture)");
            }
            else if (variant == 2)
            {
                Check(Raw.TexRefSetMipmapLevelBias(texture, 0.5f));
                float actual = -99;
                HipError result = Raw.TexRefGetMipmapLevelBias((IntPtr)(&actual), texture);
                if (result == HipError.NotSupported) Capability(result, "Raw.TexRefGetMipmapLevelBias((IntPtr)(&actual), texture)");
                else Check(result, HipError.InvalidValue, "Raw.TexRefGetMipmapLevelBias((IntPtr)(&actual), texture)");
            }
            else
            {
                Check(Raw.TexRefSetMipmapLevelClamp(texture, 0, 1));
                float lower = -99, upper = -99;
                HipError result = Raw.TexRefGetMipmapLevelClamp((IntPtr)(&lower), (IntPtr)(&upper), texture);
                if (result == HipError.NotSupported) Capability(result, "Raw.TexRefGetMipmapLevelClamp((IntPtr)(&lower), (IntPtr)(&upper), texture)");
                else Check(result, HipError.InvalidValue, "Raw.TexRefGetMipmapLevelClamp((IntPtr)(&lower), (IntPtr)(&upper), texture)");
            }
        }
        finally { if (module != 0) Check(Raw.ModuleUnload(module)); }
    }

    private static void DriverLinkLifetime()
    {
        IntPtr state = 0;
        Check(Raw.LinkCreate(0, 0, 0, (IntPtr)(&state)));
        Require(state != 0, "Driver linker state.");
        Check(Raw.LinkDestroy(state));
    }
}
