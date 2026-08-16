namespace JYPPX.ROCm.HipSharp.Rtc;

/// <summary>
/// 定义 HIPRTC linker 在 AMD 平台接受的输入种类 / Defines input kinds accepted by the HIPRTC linker on AMD platforms.
/// </summary>
public enum HipRtcJitInputType
{
    /// <summary>LLVM bitcode 或 LLVM IR assembly / LLVM bitcode or LLVM IR assembly.</summary>
    LlvmBitcode = 100,

    /// <summary>LLVM Clang bundled bitcode 输入 / LLVM Clang bundled-bitcode input.</summary>
    LlvmBundledBitcode = 101,

    /// <summary>LLVM bundled-bitcode archive 输入 / LLVM bundled-bitcode archive input.</summary>
    LlvmArchivesOfBundledBitcode = 102,

    /// <summary>SPIR-V code object 输入 / SPIR-V code-object input.</summary>
    SpirV = 103,
}
