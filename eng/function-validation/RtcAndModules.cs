using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Rtc;

internal static unsafe partial class Program
{
    private static readonly Lazy<HipRtcNativeApi> RtcApi = new(() => new HipRtcNativeApi());
    private static HipRtcNativeApi Rtc => RtcApi.Value;
    private const string Source = "extern \"C\" __global__ void Increment(int* value) { *value += 7; }";

    private sealed class Utf8 : IDisposable
    {
        internal IntPtr Pointer { get; }
        internal Utf8(string value) { Pointer = Marshal.StringToCoTaskMemUTF8(value); }
        public void Dispose() => Marshal.FreeCoTaskMem(Pointer);
    }

    private static void CheckRtc(HipRtcResult result, HipRtcResult expected = HipRtcResult.Success,
        [CallerArgumentExpression(nameof(result))] string expression = "")
    {
        var call = new { expression, result = (int)result, expected = (int)expected, phase = expected == HipRtcResult.Success ? "positive" : "negative" };
        Calls.Add(call); Console.WriteLine("NATIVE_CALL=" + System.Text.Json.JsonSerializer.Serialize(call));
        Require(result == expected, expression + ": expected " + expected + ", actual " + result);
    }

    private static byte[] Compile(bool bitcode, bool named = false)
    {
        using var source = new Utf8(Source); using var name = new Utf8("function-validation.hip");
        using var expression = new Utf8("Increment");
        using var arch = new Utf8("--offload-arch=" + (Environment.GetEnvironmentVariable("HIPSHARP_TEST_ARCH") ?? throw new InvalidOperationException("Missing architecture.")));
        using var rdc = new Utf8("-fgpu-rdc");
        IntPtr program = 0; CheckRtc(Rtc.CreateProgram((IntPtr)(&program), source.Pointer, name.Pointer, 0, IntPtr.Zero, IntPtr.Zero));
        try
        {
            if (named) CheckRtc(Rtc.AddNameExpression(program, expression.Pointer));
            IntPtr* options = stackalloc IntPtr[2] { arch.Pointer, rdc.Pointer };
            CheckRtc(Rtc.CompileProgram(program, bitcode ? 2 : 1, (IntPtr)options));
            UIntPtr logLength = 0; CheckRtc(Rtc.GetProgramLogSize(program, (IntPtr)(&logLength)));
            Require(logLength < 1024 * 1024, "Unexpected log size.");
            if (logLength > 0)
            {
                byte[] log = new byte[(int)logLength];
                fixed (byte* data = log) CheckRtc(Rtc.GetProgramLog(program, (IntPtr)data));
                Require(log[^1] == 0, "Log not terminated.");
            }
            if (named)
            {
                IntPtr lowered = 0; CheckRtc(Rtc.GetLoweredName(program, expression.Pointer, (IntPtr)(&lowered)));
                Require(Marshal.PtrToStringUTF8(lowered) == "Increment", "Extern C lowered name mismatch.");
            }
            UIntPtr length = 0;
            if (bitcode) CheckRtc(Rtc.GetBitcodeSize(program, (IntPtr)(&length)));
            else CheckRtc(Rtc.GetCodeSize(program, (IntPtr)(&length)));
            Require(length > 16 && length < 64 * 1024 * 1024, "Invalid compiler output size.");
            byte[] image = new byte[(int)length];
            fixed (byte* data = image)
            {
                if (bitcode) CheckRtc(Rtc.GetBitcode(program, (IntPtr)data));
                else CheckRtc(Rtc.GetCode(program, (IntPtr)data));
            }
            Require(bitcode ? image[0] == (byte)'B' && image[1] == (byte)'C' : image[0] == 0x7F && image[1] == (byte)'E', "Compiler output format.");
            return image;
        }
        finally { CheckRtc(Rtc.DestroyProgram((IntPtr)(&program))); }
    }

    private static void RtcCode()
    {
        int major = 0, minor = -1; CheckRtc(Rtc.Version((IntPtr)(&major), (IntPtr)(&minor))); Require(major > 0 && minor >= 0, "RTC version.");
        byte[] image = Compile(false, true); ExecuteImage(image, 0);
    }

    private static void RtcNegative()
    {
        using var source = new Utf8("extern \"C\" __global__ void Broken( { invalid; }");
        using var name = new Utf8("expected-error.hip");
        using var arch = new Utf8("--offload-arch=" + Environment.GetEnvironmentVariable("HIPSHARP_TEST_ARCH"));
        IntPtr program = 0; CheckRtc(Rtc.CreateProgram((IntPtr)(&program), source.Pointer, name.Pointer, 0, IntPtr.Zero, IntPtr.Zero));
        try
        {
            IntPtr option = arch.Pointer; CheckRtc(Rtc.CompileProgram(program, 1, (IntPtr)(&option)), HipRtcResult.Compilation);
            UIntPtr length = 0; CheckRtc(Rtc.GetProgramLogSize(program, (IntPtr)(&length))); Require(length > 1 && length < 1024 * 1024, "Missing compile diagnostics.");
            byte[] log = new byte[(int)length]; fixed (byte* data = log) CheckRtc(Rtc.GetProgramLog(program, (IntPtr)data));
            Require(System.Text.Encoding.UTF8.GetString(log).Contains("error", StringComparison.OrdinalIgnoreCase), "No error in diagnostics.");
            CheckText(Rtc.GetErrorString(HipRtcResult.Compilation));
        }
        finally { CheckRtc(Rtc.DestroyProgram((IntPtr)(&program))); }
    }

    private static void RtcLink()
    {
        byte[] bitcode = Compile(true);
        string path = Path.Combine(Environment.GetEnvironmentVariable("HIPSHARP_TEST_WORK")!, "input.bc"); File.WriteAllBytes(path, bitcode);
        for (int mode = 0; mode != 2; mode++)
        {
            IntPtr state = 0; CheckRtc(Rtc.LinkCreate(0, IntPtr.Zero, IntPtr.Zero, (IntPtr)(&state)));
            try
            {
                using var name = new Utf8(mode == 0 ? "input.bc" : path);
                if (mode == 0)
                {
                    fixed (byte* data = bitcode) CheckRtc(Rtc.LinkAddData(state, 100, (IntPtr)data, (UIntPtr)bitcode.Length, name.Pointer, 0, IntPtr.Zero, IntPtr.Zero));
                }
                else CheckRtc(Rtc.LinkAddFile(state, 100, name.Pointer, 0, IntPtr.Zero, IntPtr.Zero));
                IntPtr image = 0; UIntPtr length = 0; CheckRtc(Rtc.LinkComplete(state, (IntPtr)(&image), (IntPtr)(&length)));
                Require(image != 0 && length > 16 && length < 64 * 1024 * 1024, "Link result.");
                byte[] bytes = new byte[(int)length]; Marshal.Copy(image, bytes, 0, bytes.Length); ExecuteImage(bytes, 0);
            }
            finally { CheckRtc(Rtc.LinkDestroy(state)); }
        }
    }

    private static void Module()
    {
        byte[] image = Compile(false);
        ExecuteImage(image, 1); ExecuteImage(image, 2);
    }

    private static void ExecuteImage(byte[] image, int mode)
    {
        IntPtr module = 0, function = 0, pointer = 0;
        if (mode == 2)
        {
            string path = Path.Combine(Environment.GetEnvironmentVariable("HIPSHARP_TEST_WORK")!, "input.co"); File.WriteAllBytes(path, image);
            using var file = new Utf8(path); Check(Raw.ModuleLoad((IntPtr)(&module), file.Pointer));
        }
        else
        {
            fixed (byte* data = image)
            {
                if (mode == 0) Check(Raw.ModuleLoadData((IntPtr)(&module), (IntPtr)data));
                else Check(Raw.ModuleLoadDataEx((IntPtr)(&module), (IntPtr)data, 0, IntPtr.Zero, IntPtr.Zero));
            }
        }
        try
        {
            using var name = new Utf8("Increment"); Check(Raw.ModuleGetFunction((IntPtr)(&function), module, name.Pointer)); Require(function != 0, "Missing kernel.");
            if (mode != 0)
            {
                uint count = 0; Check(Raw.ModuleGetFunctionCount((IntPtr)(&count), module)); Require(count == 1, "Function count.");
                int limit = 0; Check(Raw.FuncGetAttribute((IntPtr)(&limit), 0, function)); Require(limit > 0, "Kernel block limit.");
                Check(Raw.FuncSetCacheConfig(function, 0)); Check(Raw.FuncSetSharedMemConfig(function, 0));
                int blocks = 0, grid = 0, blockSize = 0;
                Check(Raw.ModuleOccupancyMaxActiveBlocksPerMultiprocessor((IntPtr)(&blocks), function, 64, 0)); Require(blocks > 0, "Active occupancy.");
                Check(Raw.ModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags((IntPtr)(&blocks), function, 64, 0, 0)); Require(blocks > 0, "Flagged occupancy.");
                Check(Raw.ModuleOccupancyMaxPotentialBlockSize((IntPtr)(&grid), (IntPtr)(&blockSize), function, 0, 0)); Require(grid > 0 && blockSize > 0 && blockSize <= limit, "Potential occupancy.");
                Check(Raw.ModuleOccupancyMaxPotentialBlockSizeWithFlags((IntPtr)(&grid), (IntPtr)(&blockSize), function, 0, 0, 0)); Require(grid > 0 && blockSize > 0 && blockSize <= limit, "Flagged potential occupancy.");
            }
            Check(Raw.Malloc((IntPtr)(&pointer), sizeof(int))); int input = 35, output = 0;
            Check(Raw.MemcpyHtoD(pointer, (IntPtr)(&input), sizeof(int))); IntPtr argument = (IntPtr)(&pointer);
            Check(Raw.ModuleLaunchKernel(function, 1, 1, 1, 1, 1, 1, 0, IntPtr.Zero, (IntPtr)(&argument), IntPtr.Zero));
            Check(Raw.DeviceSynchronize()); Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == 42, "Compiled kernel bytes.");
        }
        finally { if (pointer != 0) Check(Raw.Free(pointer)); Check(Raw.ModuleUnload(module)); }
    }
}
