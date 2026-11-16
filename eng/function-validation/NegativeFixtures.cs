using System;
using JYPPX.ROCm.HipSharp.Types;
internal static unsafe partial class Program
{
    private static void NegativeMemory()
    {
        UIntPtr pitch = 0;
        Check(Raw.MallocPitch(0, (IntPtr)(&pitch), 64, 4), HipError.InvalidValue);
        Check(Raw.Malloc3D(0, new HipExtent(64, 2, 2)), HipError.InvalidValue);
        Check(Raw.Memset(0, 1, 64), HipError.InvalidValue);
        int value = 0;
        Check(Raw.Memcpy(0, (IntPtr)(&value), 4, 1), HipError.InvalidValue);
        Check(Raw.Memcpy((IntPtr)(&value), 0, 4, 2), HipError.InvalidValue);
        Check(Raw.HostGetFlags(0, 0), HipError.InvalidValue);
    }
    private static void NegativeStreams()
    {
        Check(Raw.StreamCreate(0), HipError.InvalidValue);
        Check(Raw.StreamCreateWithFlags(0, 0), HipError.InvalidValue);
        Check(Raw.StreamCreateWithPriority(0, 0, 0), HipError.InvalidValue);
        Check(Raw.StreamGetFlags(0, 0), HipError.InvalidValue);
        Check(Raw.StreamGetPriority(0, 0), HipError.InvalidValue);
    }
    private static void NegativeEvents()
    {
        Check(Raw.EventCreate(0), HipError.InvalidValue);
        Check(Raw.EventCreateWithFlags(0, 0), HipError.InvalidValue);
        IntPtr first = 0, second = 0;
        Check(Raw.EventCreate((IntPtr)(&first))); Check(Raw.EventCreate((IntPtr)(&second)));
        try
        {
            Check(Raw.EventRecord(first, 0)); Check(Raw.EventRecord(second, 0)); Check(Raw.EventSynchronize(second));
            Check(Raw.EventElapsedTime(0, first, second), HipError.InvalidValue);
        }
        finally { Check(Raw.EventDestroy(second)); Check(Raw.EventDestroy(first)); }
    }
    private static void NegativeGraphs()
    {
        Check(Raw.GraphCreate(0, 0), HipError.InvalidValue);
        IntPtr graph = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            Check(Raw.GraphAddEmptyNode(0, graph, 0, 0), HipError.InvalidValue);
            Check(Raw.GraphGetNodes(graph, 0, 0), HipError.InvalidValue);
            Check(Raw.GraphGetRootNodes(graph, 0, 0), HipError.InvalidValue);
            Check(Raw.GraphInstantiateWithFlags(0, graph, 0), HipError.InvalidValue);
        }
        finally { Check(Raw.GraphDestroy(graph)); }
    }
    private static void SelectionAndDiagnostics()
    {
        int device = 0, actual = -1;
        Check(Raw.SetValidDevices((IntPtr)(&device), 1)); Check(Raw.GetDevice((IntPtr)(&actual))); Require(actual == device, "Valid device selection.");
        IntPtr name = Raw.ApiName(98); CheckText(name, expression: "Raw.ApiName(98)");
        Require(System.Runtime.InteropServices.Marshal.PtrToStringUTF8(name) == "hipInit", "API ID/name mapping.");
    }
    private static void Profiler()
    {
        if (!Capability(Raw.ProfilerStart())) return;
        try { Check(Raw.DeviceSynchronize()); }
        finally { Check(Raw.ProfilerStop()); }
    }
    private static void ModuleGlobal()
    {
        using var source = new Utf8("extern \"C\" { __device__ int Counter = 91; __global__ void ReadCounter(int* output) { *output = ++Counter; } }");
        using var file = new Utf8("module-global.hip");
        using var arch = new Utf8("--offload-arch=" + (Environment.GetEnvironmentVariable("HIPSHARP_TEST_ARCH") ?? "gfx1100"));
        IntPtr program = 0, module = 0, pointer = 0;
        CheckRtc(Rtc.CreateProgram((IntPtr)(&program), source.Pointer, file.Pointer, 0, 0, 0));
        try
        {
            IntPtr option = arch.Pointer; CheckRtc(Rtc.CompileProgram(program, 1, (IntPtr)(&option)));
            UIntPtr length = 0; CheckRtc(Rtc.GetCodeSize(program, (IntPtr)(&length))); Require(length > 16 && length < 64 * 1024 * 1024, "Global module size.");
            byte[] image = new byte[(int)length];
            fixed (byte* bytes = image) { CheckRtc(Rtc.GetCode(program, (IntPtr)bytes)); Check(Raw.ModuleLoadData((IntPtr)(&module), (IntPtr)bytes)); }
            IntPtr global = 0, function = 0; UIntPtr size = 0;
            using var globalName = new Utf8("Counter"); using var kernelName = new Utf8("ReadCounter");
            Check(Raw.ModuleGetGlobal((IntPtr)(&global), (IntPtr)(&size), module, globalName.Pointer));
            Require(global != 0 && size == sizeof(int), "Global symbol allocation.");
            int value = 0; Check(Raw.MemcpyDtoH((IntPtr)(&value), global, sizeof(int))); Require(value == 91, "Module global initial value.");
            value = 100; Check(Raw.MemcpyHtoD(global, (IntPtr)(&value), sizeof(int)));
            Check(Raw.ModuleGetFunction((IntPtr)(&function), module, kernelName.Pointer)); Check(Raw.Malloc((IntPtr)(&pointer), sizeof(int)));
            IntPtr argument = (IntPtr)(&pointer);
            Check(Raw.ModuleLaunchKernel(function, 1, 1, 1, 1, 1, 1, 0, 0, (IntPtr)(&argument), 0)); Check(Raw.DeviceSynchronize());
            Check(Raw.MemcpyDtoH((IntPtr)(&value), pointer, sizeof(int))); Require(value == 101, "Kernel read/modified module global.");
            value = 0; Check(Raw.MemcpyDtoH((IntPtr)(&value), global, sizeof(int))); Require(value == 101, "Global address agrees with kernel state.");
        }
        finally { if (pointer != 0) Check(Raw.Free(pointer)); if (module != 0) Check(Raw.ModuleUnload(module)); CheckRtc(Rtc.DestroyProgram((IntPtr)(&program))); }
    }
}
