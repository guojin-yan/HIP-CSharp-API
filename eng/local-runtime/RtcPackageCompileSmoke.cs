using System;
using System.Security.Cryptography;
using JYPPX.ROCm.HipSharp.Rtc;

// Package-only compiler check. Successful code generation is not GPU execution.
internal static class Program
{
    private static int Main(string[] args)
    {
        var rtc = new HipRtc();
        using HipRtcProgram program = rtc.CreateProgram(
            "extern \"C\" __global__ void Increment(int* value) { value[0] += 1; }", "package-compile.hip");
        HipRtcCompilation compilation = program.Compile(new[] { "--offload-arch=" + args[0], "-O2" });
        byte[] code = compilation.GetCodeObject();
        if (code.Length < 4 || code[0] != 0x7f || code[1] != 'E' || code[2] != 'L' || code[3] != 'F')
            throw new InvalidOperationException("HIPRTC did not produce an ELF code object.");
        Console.WriteLine("HIPRTC=" + rtc.GetVersion() + "; codeBytes=" + code.Length +
            "; sha256=" + Convert.ToHexString(SHA256.HashData(code)).ToLowerInvariant() + "; gpuExecuted=false");
        return 0;
    }
}
