using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    [StructLayout(LayoutKind.Sequential)] private struct Copy2D
    {
        public UIntPtr SourceX, SourceY;
        public int SourceType;
        public IntPtr SourceHost, SourceDevice, SourceArray;
        public UIntPtr SourcePitch, DestinationX, DestinationY;
        public int DestinationType;
        public IntPtr DestinationHost, DestinationDevice, DestinationArray;
        public UIntPtr DestinationPitch, Width, Height;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Copy3D
    {
        public UIntPtr SourceX, SourceY, SourceZ, SourceLod;
        public int SourceType;
        public IntPtr SourceHost, SourceDevice, SourceArray;
        public UIntPtr SourcePitch, SourceHeight, DestinationX, DestinationY, DestinationZ, DestinationLod;
        public int DestinationType;
        public IntPtr DestinationHost, DestinationDevice, DestinationArray;
        public UIntPtr DestinationPitch, DestinationHeight, Width, Height, Depth;
    }

    private static void DriverCopy()
    {
        IntPtr device = 0, host = 0, stream = 0; UIntPtr pitch = 0;
        Check(Raw.MemAllocPitch((IntPtr)(&device), (IntPtr)(&pitch), 64, 4, 4));
        Require(pitch >= 64, "Driver allocation pitch.");
        try
        {
            Check(Raw.HostMalloc((IntPtr)(&host), 256, 0)); Check(Raw.StreamCreate((IntPtr)(&stream)));
            for (int i = 0; i < 256; i++) ((byte*)host)[i] = (byte)(i * 37 + 11);
            Copy2D upload = new() { SourceType = 1, SourceHost = host, SourcePitch = 64,
                DestinationType = 2, DestinationDevice = device, DestinationPitch = pitch, Width = 64, Height = 4 };
            Copy2D download = new() { SourceType = 2, SourceDevice = device, SourcePitch = pitch,
                DestinationType = 1, DestinationHost = host, DestinationPitch = 64, Width = 64, Height = 4 };
            Check(Raw.MemcpyParam2D((IntPtr)(&upload))); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.DrvMemcpy2DUnaligned((IntPtr)(&download))); VerifyPattern(host, 256);
            Check(Raw.MemcpyParam2DAsync((IntPtr)(&upload), stream)); Check(Raw.StreamSynchronize(stream)); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.MemcpyParam2DAsync((IntPtr)(&download), stream)); Check(Raw.StreamSynchronize(stream)); VerifyPattern(host, 256);
            Copy3D upload3D = new() { SourceType = 1, SourceHost = host, SourcePitch = 64, SourceHeight = 2,
                DestinationType = 2, DestinationDevice = device, DestinationPitch = pitch, DestinationHeight = 2, Width = 64, Height = 2, Depth = 2 };
            Copy3D download3D = new() { SourceType = 2, SourceDevice = device, SourcePitch = pitch, SourceHeight = 2,
                DestinationType = 1, DestinationHost = host, DestinationPitch = 64, DestinationHeight = 2, Width = 64, Height = 2, Depth = 2 };
            Check(Raw.DrvMemcpy3D((IntPtr)(&upload3D))); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.DrvMemcpy3D((IntPtr)(&download3D))); VerifyPattern(host, 256);
            Check(Raw.DrvMemcpy3DAsync((IntPtr)(&upload3D), stream)); Check(Raw.StreamSynchronize(stream)); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.DrvMemcpy3DAsync((IntPtr)(&download3D), stream)); Check(Raw.StreamSynchronize(stream)); VerifyPattern(host, 256);
            DriverGraph(upload3D, download3D, host);
        }
        finally
        {
            if (stream != 0) Check(Raw.StreamDestroy(stream)); if (host != 0) Check(Raw.HostFree(host)); Check(Raw.Free(device));
        }
    }

    private static void DriverGraph(Copy3D upload, Copy3D download, IntPtr host)
    {
        IntPtr graph = 0, executable = 0, node = 0, context = 0;
        Check(Raw.CtxGetCurrent((IntPtr)(&context))); Require(context != 0, "Missing allocation context.");
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            Check(Raw.DrvGraphAddMemcpyNode((IntPtr)(&node), graph, IntPtr.Zero, 0, (IntPtr)(&upload), context));
            Copy3D query = default; Check(Raw.DrvGraphMemcpyNodeGetParams(node, (IntPtr)(&query)));
            Require(query.SourceHost == upload.SourceHost && query.DestinationDevice == upload.DestinationDevice && query.Width == 64 && query.Depth == 2, "Driver graph copy query.");
            Check(Raw.DrvGraphMemcpyNodeSetParams(node, (IntPtr)(&download)));
            Check(Raw.DrvGraphMemcpyNodeGetParams(node, (IntPtr)(&query))); Require(query.DestinationHost == host && query.SourceDevice == download.SourceDevice, "Driver graph copy update.");
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            new Span<byte>((void*)host, 256).Clear();
            Check(Raw.DrvGraphExecMemcpyNodeSetParams(executable, node, (IntPtr)(&download), context));
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize()); VerifyPattern(host, 256);
        }
        finally { if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); }
    }
}
