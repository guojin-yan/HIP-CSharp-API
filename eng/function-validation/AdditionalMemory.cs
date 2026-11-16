using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    private static void RuntimeGraphCopy()
    {
        IntPtr graph = 0, executable = 0, node = 0, device = 0, host = 0;
        Check(Raw.Malloc((IntPtr)(&device), 256)); Check(Raw.HostMalloc((IntPtr)(&host), 512, 0));
        try
        {
            for (int i = 0; i < 256; i++) ((byte*)host)[i] = (byte)(i * 37 + 11);
            Check(Raw.GraphCreate((IntPtr)(&graph), 0));
            RuntimeCopy3D upload = new() { Source = new HipPitchedPtr(host, 64, 64, 2), Destination = new HipPitchedPtr(device, 64, 64, 2), Extent = new HipExtent(64, 2, 2), Kind = 1 };
            Check(Raw.GraphAddMemcpyNode((IntPtr)(&node), graph, 0, 0, (IntPtr)(&upload)));
            RuntimeCopy3D query = default; Check(Raw.GraphMemcpyNodeGetParams(node, (IntPtr)(&query)));
            Require(query.Source.Address == host && query.Destination.Address == device && query.Extent.Width == 64 && query.Extent.Depth == 2, "Runtime copy node parameters.");
            Check(Raw.GraphMemcpyNodeSetParams(node, (IntPtr)(&upload)));
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize());
            Check(Raw.MemcpyDtoH(host + 256, device, 256)); VerifyPattern(host + 256, 256);
            RuntimeCopy3D download = new() { Source = upload.Destination, Destination = new HipPitchedPtr(host + 256, 64, 64, 2), Extent = upload.Extent, Kind = 2 };
            // Preserve transfer kind for executable updates: change the host source, then verify new bytes.
            new Span<byte>((void*)host, 256).Fill(0x71);
            Check(Raw.GraphExecMemcpyNodeSetParams(executable, node, (IntPtr)(&upload)));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); Check(Raw.Memcpy3D((IntPtr)(&download)));
            Require(new ReadOnlySpan<byte>((void*)(host + 256), 256).IndexOfAnyExcept((byte)0x71) == -1, "Executable runtime copy update.");
        }
        finally { if (executable != 0) Check(Raw.GraphExecDestroy(executable)); if (graph != 0) Check(Raw.GraphDestroy(graph)); Check(Raw.HostFree(host)); Check(Raw.Free(device)); }
    }

    private static void DriverGraphMemset()
    {
        IntPtr pointer = 0, context = 0, graph = 0, executable = 0, node = 0;
        Check(Raw.Malloc((IntPtr)(&pointer), 256)); Check(Raw.CtxGetCurrent((IntPtr)(&context)));
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            MemsetParameters parameters = new() { Destination = pointer, ElementSize = 1, Height = 1, Width = 256, Value = 0x43 };
            Check(Raw.DrvGraphAddMemsetNode((IntPtr)(&node), graph, 0, 0, (IntPtr)(&parameters), context));
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); VerifyFill(pointer, 1, 0x43);
            parameters.Value = 0x54; Check(Raw.DrvGraphExecMemsetNodeSetParams(executable, node, (IntPtr)(&parameters), context));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); VerifyFill(pointer, 1, 0x54);
        }
        finally { if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); Check(Raw.Free(pointer)); }
    }

    private static void StreamValues()
    {
        IntPtr pointer = 0, stream = 0;
        if (!Capability(Raw.ExtMallocWithFlags((IntPtr)(&pointer), 8, 2))) return;
        try
        {
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            if (!Capability(Raw.StreamWriteValue32(stream, pointer, 0x12345678, 0))) return;
            Check(Raw.StreamWaitValue32(stream, pointer, 0x12345678, 1, uint.MaxValue));
            Check(Raw.StreamSynchronize(stream)); uint value = 0;
            Check(Raw.MemcpyDtoH((IntPtr)(&value), pointer, 4)); Require(value == 0x12345678, "Stream 32-bit write/wait.");
            Check(Raw.StreamWriteValue64(stream, pointer, 0x123456789ABCDEF0, 0));
            Check(Raw.StreamWaitValue64(stream, pointer, 0x123456789ABCDEF0, 1, ulong.MaxValue));
            Check(Raw.StreamSynchronize(stream)); ulong wide = 0;
            Check(Raw.MemcpyDtoH((IntPtr)(&wide), pointer, 8)); Require(wide == 0x123456789ABCDEF0, "Stream 64-bit write/wait.");
        }
        finally { if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.Free(pointer)); }
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)] private struct BatchOperation
    {
        [FieldOffset(0)] public int Operation;
        [FieldOffset(8)] public IntPtr Address;
        [FieldOffset(16)] public ulong Value;
        [FieldOffset(24)] public uint Flags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BatchNode
    {
        public IntPtr Context;
        public uint Count;
        public IntPtr Parameters;
        public uint Flags;
    }
    private static void BatchMemory(bool graphMode)
    {
        IntPtr pointer = 0, stream = 0, graph = 0, executable = 0, node = 0;
        if (!Capability(Raw.ExtMallocWithFlags((IntPtr)(&pointer), 8, 2))) return;
        try
        {
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            BatchOperation* operations = stackalloc BatchOperation[2];
            operations[0] = new() { Operation = 5, Address = pointer, Value = 0x123456789ABCDEF0 };
            operations[1] = new() { Operation = 4, Address = pointer, Value = operations[0].Value, Flags = 1 };
            if (!graphMode)
            {
                if (!Capability(Raw.StreamBatchMemOp(stream, 2, (IntPtr)operations, 0))) return;
            }
            else
            {
                IntPtr context = 0; Check(Raw.CtxGetCurrent((IntPtr)(&context))); Check(Raw.GraphCreate((IntPtr)(&graph), 0));
                BatchNode parameters = new() { Context = context, Count = 2, Parameters = (IntPtr)operations };
                if (!Capability(Raw.GraphAddBatchMemOpNode((IntPtr)(&node), graph, 0, 0, (IntPtr)(&parameters)))) return;
                BatchNode query = default; Check(Raw.GraphBatchMemOpNodeGetParams(node, (IntPtr)(&query)));
                Require(query.Context == context && query.Count == 2 && query.Flags == 0 && query.Parameters != 0, "Batch graph parameters.");
                var copied = (BatchOperation*)query.Parameters;
                Require(copied[0].Address == pointer && copied[0].Value == operations[0].Value && copied[1].Operation == 4, "Batch operation round trip.");
                Check(Raw.GraphBatchMemOpNodeSetParams(node, (IntPtr)(&parameters)));
                Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
                Check(Raw.GraphLaunch(executable, stream)); Check(Raw.StreamSynchronize(stream));
                ulong first = 0; Check(Raw.MemcpyDtoH((IntPtr)(&first), pointer, 8)); Require(first == operations[0].Value, $"Initial graph batch result: expected {operations[0].Value:X16}, actual {first:X16}.");
                operations[0].Value = operations[1].Value = 0x23456789ABCDEF01;
                Check(Raw.GraphExecBatchMemOpNodeSetParams(executable, node, (IntPtr)(&parameters)));
                Check(Raw.GraphLaunch(executable, stream));
            }
            Check(Raw.StreamSynchronize(stream)); ulong value = 0;
            Check(Raw.MemcpyDtoH((IntPtr)(&value), pointer, 8)); Require(value == operations[0].Value, $"Batch memory result: expected {operations[0].Value:X16}, actual {value:X16}.");
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); if (graph != 0) Check(Raw.GraphDestroy(graph));
            if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.Free(pointer));
        }
    }

    private static void CaptureToGraph()
    {
        IntPtr graph = 0, returned = 0, executable = 0, stream = 0, root = 0, pointer = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0)); Check(Raw.StreamCreateWithFlags((IntPtr)(&stream), 1)); Check(Raw.Malloc((IntPtr)(&pointer), 256));
        try
        {
            Check(Raw.GraphAddEmptyNode((IntPtr)(&root), graph, 0, 0));
            if (!Capability(Raw.StreamBeginCaptureToGraph(stream, graph, (IntPtr)(&root), 0, 1, 0))) return;
            Check(Raw.MemsetAsync(pointer, 0x61, 256, stream)); Check(Raw.StreamEndCapture(stream, (IntPtr)(&returned)));
            Require(returned == graph, "Capture did not extend the supplied graph.");
            UIntPtr count = 0; Check(Raw.GraphGetNodes(graph, 0, (IntPtr)(&count))); Require(count == 2, "Capture graph node count.");
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 1, 0x61);
        }
        finally { if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); Check(Raw.StreamDestroy(stream)); Check(Raw.Free(pointer)); }
    }

    private static void PrimaryLifetime()
    {
        IntPtr context = 0; Check(Raw.DevicePrimaryCtxReset(0));
        Check(Raw.DevicePrimaryCtxRetain((IntPtr)(&context), 0)); Require(context != 0, "Primary context retain.");
        try { int device = -1; Check(Raw.CtxSetCurrent(context)); Check(Raw.CtxGetDevice((IntPtr)(&device))); Require(device == 0, "Primary context identity."); }
        finally { Check(Raw.DevicePrimaryCtxRelease(0)); Check(Raw.DevicePrimaryCtxReset(0)); }
    }

    private static void DeviceReset()
    {
        Check(Raw.DeviceSynchronize()); Check(Raw.DeviceReset());
        Check(Raw.SetDevice(0)); IntPtr pointer = 0; Check(Raw.Malloc((IntPtr)(&pointer), 256));
        try { Check(Raw.Memset(pointer, 0x38, 256)); VerifyFill(pointer, 1, 0x38); }
        finally { Check(Raw.Free(pointer)); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct CopyOperand
    {
        public int Type;
        public IntPtr Pointer;
        public UIntPtr RowLength, LayerHeight;
        public HipMemLocation Location;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BatchCopy3D
    {
        public CopyOperand Source, Destination;
        public HipExtent Extent;
        public int SourceOrder;
        public uint Flags;
    }
    private static void BatchCopies(bool threeDimensional)
    {
        IntPtr host = 0, device = 0, stream = 0;
        Check(Raw.HostMalloc((IntPtr)(&host), 512, 0)); Check(Raw.Malloc((IntPtr)(&device), 256));
        try
        {
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            for (int i = 0; i < 256; i++) ((byte*)host)[i] = (byte)(i * 37 + 11);
            if (!threeDimensional)
            {
                IntPtr* sources = stackalloc IntPtr[2] { host, host + 128 };
                IntPtr* destinations = stackalloc IntPtr[2] { device, device + 128 };
                UIntPtr* sizes = stackalloc UIntPtr[2] { 128, 128 }; UIntPtr failure = UIntPtr.MaxValue;
                if (!Capability(Raw.MemcpyBatchAsync((IntPtr)destinations, (IntPtr)sources, (IntPtr)sizes, 2, 0, 0, 0, (IntPtr)(&failure), stream))) return;
                Check(Raw.StreamSynchronize(stream));
                sources[0] = device; sources[1] = device + 128; destinations[0] = host + 256; destinations[1] = host + 384;
                Check(Raw.MemcpyBatchAsync((IntPtr)destinations, (IntPtr)sources, (IntPtr)sizes, 2, 0, 0, 0, (IntPtr)(&failure), stream));
            }
            else
            {
                BatchCopy3D operation = new() {
                    Source = new CopyOperand { Type = 1, Pointer = host, RowLength = 64, LayerHeight = 2, Location = new HipMemLocation(2, 0) },
                    Destination = new CopyOperand { Type = 1, Pointer = device, RowLength = 64, LayerHeight = 2, Location = new HipMemLocation(1, 0) },
                    Extent = new HipExtent(64, 2, 2), SourceOrder = 1 };
                UIntPtr failure = UIntPtr.MaxValue;
                if (!Capability(Raw.Memcpy3DBatchAsync(1, (IntPtr)(&operation), (IntPtr)(&failure), 0, stream))) return;
                Check(Raw.StreamSynchronize(stream));
                operation.Source = operation.Destination;
                operation.Destination = new CopyOperand { Type = 1, Pointer = host + 256, RowLength = 64, LayerHeight = 2, Location = new HipMemLocation(2, 0) };
                Check(Raw.Memcpy3DBatchAsync(1, (IntPtr)(&operation), (IntPtr)(&failure), 0, stream));
            }
            Check(Raw.StreamSynchronize(stream)); VerifyPattern(host + 256, 256);
        }
        finally { if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.Free(device)); Check(Raw.HostFree(host)); }
    }
}
