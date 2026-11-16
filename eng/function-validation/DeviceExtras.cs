using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;
internal static unsafe partial class Program
{
    // Unversioned hipGetDeviceProperties/hipChooseDevice use the legacy R0000 ABI.
    // The exact installed header layout is checked in host-kernel.hip before these fixtures run.
    private static void LegacyDeviceProperties()
    {
        LoadHelperPointer("ValidationKernel");
        const int size = 792;
        byte* properties = stackalloc byte[size + 32]; new Span<byte>(properties, size).Clear(); new Span<byte>(properties + size, 32).Fill(0xCD);
        Check(Raw.GetDeviceProperties((IntPtr)properties, 0));
        Require(new ReadOnlySpan<byte>(properties + size, 32).IndexOfAnyExcept((byte)0xCD) == -1, "Legacy property ABI guard.");
        byte* name = stackalloc byte[256]; Check(Raw.DeviceGetName((IntPtr)name, 256, 0));
        Require(Marshal.PtrToStringUTF8((IntPtr)properties) == Marshal.PtrToStringUTF8((IntPtr)name), "Legacy device name.");
        UIntPtr memory = 0; Check(Raw.DeviceTotalMem((IntPtr)(&memory), 0)); Require(*(UIntPtr*)(properties + 256) == memory, "Legacy global memory.");
        int major = 0, minor = 0, units = 0;
        Check(Raw.DeviceComputeCapability((IntPtr)(&major), (IntPtr)(&minor), 0)); Check(Raw.DeviceGetAttribute((IntPtr)(&units), 63, 0));
        Require(*(int*)(properties + 328) == major && *(int*)(properties + 332) == minor && *(int*)(properties + 336) == units, "Legacy capability and unit layout.");
        int selected = -1; Check(Raw.ChooseDevice((IntPtr)(&selected), (IntPtr)properties)); Require(selected == 0, "Legacy device selection.");
    }
    private static void PeerSingleDevice()
    {
        int count = 0; Check(Raw.GetDeviceCount((IntPtr)(&count))); Require(count > 0, "Peer query device count.");
        for (int device = 0; device < count; device++)
        {
            int access = -1; Check(Raw.DeviceCanAccessPeer((IntPtr)(&access), device, device)); Require(access == 0, "Device cannot peer with itself.");
        }
        int value = 0; Check(Raw.DeviceGetP2PAttribute((IntPtr)(&value), 0, -1, 0), HipError.InvalidDevice);
    }
    private static void StreamAttach()
    {
        IntPtr stream = 0, pointer = 0;
        Check(Raw.Malloc((IntPtr)(&pointer), 256));
        try
        {
            Check(Raw.StreamCreateWithFlags((IntPtr)(&stream), 1));
            if (!Capability(Raw.StreamAttachMemAsync(stream, pointer, 0, 1))) return;
            Check(Raw.MemsetAsync(pointer, 0x47, 256, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 1, 0x47);
        }
        finally { if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.Free(pointer)); }
    }
    private static void ProfilerStopOnly()
    {
        Capability(Raw.ProfilerStop());
    }
    private static void ManagedBasic()
    {
        IntPtr pointer = 0, stream = 0;
        if (!Capability(Raw.MallocManaged((IntPtr)(&pointer), 4096, 1))) return;
        try
        {
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            int value = 41, output = 0;
            Check(Raw.MemcpyHtoD(pointer, (IntPtr)(&value), sizeof(int)));
            if (Capability(Raw.MemPrefetchAsync(pointer, 4096, 0, stream))) Check(Raw.StreamSynchronize(stream));
            if (Capability(Raw.MemPrefetchAsyncV2(pointer, 4096, new HipMemLocation(1, 0), 0, stream))) Check(Raw.StreamSynchronize(stream));
            Check(Raw.MemcpyDtoH((IntPtr)(&output), pointer, sizeof(int))); Require(output == value, "Basic managed allocation and prefetch bytes.");
            // This deliberately does not establish direct CPU coherence or memory-advice support.
        }
        finally { if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.Free(pointer)); }
    }
}
