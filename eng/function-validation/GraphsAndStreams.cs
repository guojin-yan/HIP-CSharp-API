using System;
using System.Runtime.InteropServices;
using System.Threading;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    private static void StreamEvent()
    {
        IntPtr first = 0, second = 0, start = 0, stop = 0; int priority = int.MaxValue, device = -1; uint flags = 0;
        Check(Raw.StreamCreateWithPriority((IntPtr)(&first), 1, 0));
        try
        {
            Check(Raw.StreamCreateWithFlags((IntPtr)(&second), 1));
            Check(Raw.StreamGetFlags(first, (IntPtr)(&flags))); Require(flags == 1, "Stream flags.");
            Check(Raw.StreamGetPriority(first, (IntPtr)(&priority))); Require(priority == 0, "Stream priority.");
            Check(Raw.StreamGetDevice(first, (IntPtr)(&device))); Require(device == 0, "Stream device.");
            Require(Raw.GetStreamDeviceId(first) == device, "Stream device ID differs.");
            ulong firstId = ulong.MaxValue, secondId = ulong.MaxValue;
            Check(Raw.StreamGetId(first, (IntPtr)(&firstId))); Check(Raw.StreamGetId(second, (IntPtr)(&secondId)));
            Require(firstId != secondId, "Stream IDs not unique.");
            Check(Raw.StreamCopyAttributes(second, first));
            Check(Raw.EventCreate((IntPtr)(&start))); Check(Raw.EventCreateWithFlags((IntPtr)(&stop), 0));
            Check(Raw.EventRecord(start, first)); Check(Raw.StreamWaitEvent(second, start, 0));
            Check(Raw.EventRecordWithFlags(stop, second, 0)); Check(Raw.EventSynchronize(stop));
            Check(Raw.EventQuery(start)); Check(Raw.EventQuery(stop));
            float elapsed = -1; Check(Raw.EventElapsedTime((IntPtr)(&elapsed), start, stop));
            Require(float.IsFinite(elapsed) && elapsed >= 0, "Event timing.");
            Check(Raw.StreamSynchronize(first)); Check(Raw.StreamSynchronize(second));
            Check(Raw.StreamQuery(first)); Check(Raw.StreamQuery(second));
        }
        finally
        {
            if (stop != 0) Check(Raw.EventDestroy(stop)); if (start != 0) Check(Raw.EventDestroy(start));
            if (second != 0) Check(Raw.StreamDestroy(second)); Check(Raw.StreamDestroy(first));
        }
    }

    private static void Capture()
    {
        IntPtr stream = 0, graph = 0, executable = 0, pointer = 0;
        Check(Raw.StreamCreateWithFlags((IntPtr)(&stream), 1));
        try
        {
            Check(Raw.Malloc((IntPtr)(&pointer), 256));
            int status = -1; ulong id = 0, id2 = 0;
            Check(Raw.StreamIsCapturing(stream, (IntPtr)(&status))); Require(status == 0, "Unexpected capture.");
            Check(Raw.StreamBeginCapture(stream, 0));
            Check(Raw.StreamGetCaptureInfo(stream, (IntPtr)(&status), (IntPtr)(&id)));
            Require(status == 1 && id != 0, "Capture status/id.");
            Check(Raw.MemsetAsync(pointer, 0x5A, 256, stream));
            IntPtr capturedGraph = 0, dependencies = 0; UIntPtr count = 0;
            Check(Raw.StreamGetCaptureInfoV2(stream, (IntPtr)(&status), (IntPtr)(&id2),
                (IntPtr)(&capturedGraph), (IntPtr)(&dependencies), (IntPtr)(&count)));
            Require(status == 1 && id == id2 && capturedGraph != 0 && count == 1 && dependencies != 0, "Capture v2 frontier.");
            Check(Raw.StreamUpdateCaptureDependencies(stream, dependencies, count, 1));
            Check(Raw.StreamEndCapture(stream, (IntPtr)(&graph))); Require(graph == capturedGraph, "Captured graph mismatch.");
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 1, 0x5A);
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); if (graph != 0) Check(Raw.GraphDestroy(graph));
            if (pointer != 0) Check(Raw.Free(pointer)); Check(Raw.StreamDestroy(stream));
        }
    }

    private static void GraphTopology()
    {
        IntPtr graph = 0, clone = 0, executable = 0, first = 0, second = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            Check(Raw.GraphAddEmptyNode((IntPtr)(&first), graph, IntPtr.Zero, 0));
            Check(Raw.GraphAddEmptyNode((IntPtr)(&second), graph, IntPtr.Zero, 0));
            Check(Raw.GraphAddDependencies(graph, (IntPtr)(&first), (IntPtr)(&second), 1));
            UIntPtr count = 0; Check(Raw.GraphGetNodes(graph, IntPtr.Zero, (IntPtr)(&count))); Require(count == 2, "Node count.");
            IntPtr* nodes = stackalloc IntPtr[2]; Check(Raw.GraphGetNodes(graph, (IntPtr)nodes, (IntPtr)(&count)));
            Require(nodes[0] != nodes[1] && (nodes[0] == first || nodes[1] == first) && (nodes[0] == second || nodes[1] == second), "Node set.");
            count = 2; Check(Raw.GraphGetRootNodes(graph, (IntPtr)nodes, (IntPtr)(&count))); Require(count == 1 && nodes[0] == first, "Root set.");
            IntPtr from = 0, to = 0; count = 1;
            Check(Raw.GraphGetEdges(graph, (IntPtr)(&from), (IntPtr)(&to), (IntPtr)(&count)));
            Require(count == 1 && from == first && to == second, "Edge set.");
            count = 1; Check(Raw.GraphNodeGetDependencies(second, (IntPtr)nodes, (IntPtr)(&count))); Require(count == 1 && nodes[0] == first, "Dependencies.");
            count = 1; Check(Raw.GraphNodeGetDependentNodes(first, (IntPtr)nodes, (IntPtr)(&count))); Require(count == 1 && nodes[0] == second, "Dependents.");
            int type = -1; Check(Raw.GraphNodeGetType(first, (IntPtr)(&type))); Require(type == 5, "Expected empty node.");
            Check(Raw.GraphClone((IntPtr)(&clone), graph));
            IntPtr clonedNode = 0; Check(Raw.GraphNodeFindInClone((IntPtr)(&clonedNode), first, clone)); Require(clonedNode != 0 && clonedNode != first, "Clone node identity.");
            Check(Raw.GraphInstantiate((IntPtr)(&executable), graph, IntPtr.Zero, IntPtr.Zero, 0));
            ulong flags = ulong.MaxValue; Check(Raw.GraphExecGetFlags(executable, (IntPtr)(&flags))); Require(flags == 0, "Executable flags.");
            Check(Raw.GraphUpload(executable, IntPtr.Zero)); Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize());
            Check(Raw.GraphExecDestroy(executable)); executable = 0;
            Check(Raw.GraphRemoveDependencies(graph, (IntPtr)(&first), (IntPtr)(&second), 1));
            count = 2; Check(Raw.GraphGetRootNodes(graph, (IntPtr)nodes, (IntPtr)(&count))); Require(count == 2, "Edge removal.");
            Check(Raw.GraphDestroyNode(second)); count = 0;
            Check(Raw.GraphGetNodes(graph, IntPtr.Zero, (IntPtr)(&count))); Require(count == 1, "Node removal.");
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); if (clone != 0) Check(Raw.GraphDestroy(clone)); Check(Raw.GraphDestroy(graph));
        }
    }

    private static void GraphEvents()
    {
        IntPtr graph = 0, executable = 0, first = 0, second = 0, record = 0, wait = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            Check(Raw.EventCreate((IntPtr)(&first))); Check(Raw.EventCreate((IntPtr)(&second)));
            Check(Raw.GraphAddEventRecordNode((IntPtr)(&record), graph, IntPtr.Zero, 0, first));
            Check(Raw.GraphAddEventWaitNode((IntPtr)(&wait), graph, (IntPtr)(&record), 1, first));
            IntPtr queried = 0; Check(Raw.GraphEventRecordNodeGetEvent(record, (IntPtr)(&queried))); Require(queried == first, "Record event query.");
            Check(Raw.GraphEventWaitNodeGetEvent(wait, (IntPtr)(&queried))); Require(queried == first, "Wait event query.");
            Check(Raw.GraphEventRecordNodeSetEvent(record, second)); Check(Raw.GraphEventWaitNodeSetEvent(wait, second));
            Check(Raw.GraphEventRecordNodeGetEvent(record, (IntPtr)(&queried))); Require(queried == second, "Record event update.");
            Check(Raw.GraphEventWaitNodeGetEvent(wait, (IntPtr)(&queried))); Require(queried == second, "Wait event update.");
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphExecEventRecordNodeSetEvent(executable, record, first));
            Check(Raw.GraphExecEventWaitNodeSetEvent(executable, wait, first));
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize()); Check(Raw.EventQuery(first));
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph));
            if (second != 0) Check(Raw.EventDestroy(second)); if (first != 0) Check(Raw.EventDestroy(first));
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void HostFunction(IntPtr userData);
    [StructLayout(LayoutKind.Sequential)] private struct HostParameters { public IntPtr Function; public IntPtr UserData; }

    private static void GraphHost()
    {
        int calls = 0; IntPtr observed = 0;
        HostFunction callback = data => { observed = data; Interlocked.Increment(ref calls); };
        HostParameters parameters = new() { Function = Marshal.GetFunctionPointerForDelegate(callback), UserData = (IntPtr)42 };
        IntPtr graph = 0, executable = 0, node = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            Check(Raw.GraphAddHostNode((IntPtr)(&node), graph, IntPtr.Zero, 0, (IntPtr)(&parameters)));
            HostParameters query = default;
            Check(Raw.GraphHostNodeGetParams(node, (IntPtr)(&query)));
            Require(query.Function == parameters.Function && query.UserData == parameters.UserData, "Host parameters.");
            parameters.UserData = (IntPtr)43; Check(Raw.GraphHostNodeSetParams(node, (IntPtr)(&parameters)));
            Check(Raw.GraphHostNodeGetParams(node, (IntPtr)(&query))); Require(query.UserData == (IntPtr)43, "Host update.");
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize()); Require(calls == 1 && observed == (IntPtr)43, "Host execution.");
            parameters.UserData = (IntPtr)44; Check(Raw.GraphExecHostNodeSetParams(executable, node, (IntPtr)(&parameters)));
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize()); Require(calls == 2 && observed == (IntPtr)44, "Executable host update.");
            // HIP only permits enabling/disabling kernel, memcpy and memset nodes.
            Check(Raw.GraphNodeSetEnabled(executable, node, 0), HipError.InvalidValue);
        }
        finally { if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); GC.KeepAlive(callback); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemsetParameters
    {
        public IntPtr Destination;
        public uint ElementSize;
        public UIntPtr Height, Pitch;
        public uint Value;
        public UIntPtr Width;
    }

    private static void GraphMemory()
    {
        IntPtr graph = 0, executable = 0, pointer = 0, host = 0, setNode = 0, copyNode = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            Check(Raw.Malloc((IntPtr)(&pointer), 256)); Check(Raw.HostMalloc((IntPtr)(&host), 256, 0));
            MemsetParameters parameters = new() { Destination = pointer, ElementSize = 1, Height = 1, Value = 0x11, Width = 256 };
            Check(Raw.GraphAddMemsetNode((IntPtr)(&setNode), graph, IntPtr.Zero, 0, (IntPtr)(&parameters)));
            MemsetParameters query = default;
            Check(Raw.GraphMemsetNodeGetParams(setNode, (IntPtr)(&query)));
            Require(query.Destination == pointer && query.Value == 0x11 && query.Width == 256 && query.ElementSize == 1, "Memset node parameters.");
            parameters.Value = 0x22; Check(Raw.GraphMemsetNodeSetParams(setNode, (IntPtr)(&parameters)));
            Check(Raw.GraphMemsetNodeGetParams(setNode, (IntPtr)(&query))); Require(query.Value == 0x22, "Memset update.");
            Check(Raw.GraphAddMemcpyNode1D((IntPtr)(&copyNode), graph, (IntPtr)(&setNode), 1, host, pointer, 256, 2));
            Check(Raw.GraphMemcpyNodeSetParams1D(copyNode, host, pointer, 128, 2));
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphExecMemcpyNodeSetParams1D(executable, copyNode, host, pointer, 256, 2));
            new Span<byte>((void*)host, 256).Fill(0xCD);
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize());
            Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x22) == -1, "Graph memory result.");
            parameters.Value = 0x33; Check(Raw.GraphExecMemsetNodeSetParams(executable, setNode, (IntPtr)(&parameters)));
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize());
            Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x33) == -1, "Graph executable memset update.");
            Check(Raw.GraphNodeSetEnabled(executable, copyNode, 0)); uint enabled = 1;
            Check(Raw.GraphNodeGetEnabled(executable, copyNode, (IntPtr)(&enabled))); Require(enabled == 0, "Memcpy disabled query.");
            new Span<byte>((void*)host, 256).Fill(0xCD);
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize());
            Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0xCD) == -1, "Disabled memcpy executed.");
            Check(Raw.GraphNodeSetEnabled(executable, copyNode, 1));
            Check(Raw.GraphNodeGetEnabled(executable, copyNode, (IntPtr)(&enabled))); Require(enabled == 1, "Memcpy enabled query.");
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize());
            Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x33) == -1, "Enabled memcpy did not execute.");
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph));
            if (host != 0) Check(Raw.HostFree(host)); if (pointer != 0) Check(Raw.Free(pointer));
        }
    }

    private static void GraphChild()
    {
        int calls = 0;
        HostFunction callback = data => Interlocked.Add(ref calls, data.ToInt32());
        HostParameters parameters = new() { Function = Marshal.GetFunctionPointerForDelegate(callback), UserData = (IntPtr)1 };
        IntPtr child = 0, parent = 0, executable = 0, node = 0, hostNode = 0, replacement = 0;
        Check(Raw.GraphCreate((IntPtr)(&parent), 0));
        try
        {
            Check(Raw.GraphCreate((IntPtr)(&child), 0));
            Check(Raw.GraphAddHostNode((IntPtr)(&hostNode), child, IntPtr.Zero, 0, (IntPtr)(&parameters)));
            Check(Raw.GraphAddChildGraphNode((IntPtr)(&node), parent, IntPtr.Zero, 0, child));
            IntPtr query = 0; Check(Raw.GraphChildGraphNodeGetGraph(node, (IntPtr)(&query))); Require(query != 0, "Missing child graph.");
            UIntPtr count = 0; Check(Raw.GraphGetNodes(query, IntPtr.Zero, (IntPtr)(&count))); Require(count == 1, "Cloned child nodes.");
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), parent, 0));
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize()); Require(calls == 1, "Child execution.");
            Check(Raw.GraphCreate((IntPtr)(&replacement), 0)); parameters.UserData = (IntPtr)2;
            Check(Raw.GraphAddHostNode((IntPtr)(&hostNode), replacement, IntPtr.Zero, 0, (IntPtr)(&parameters)));
            Check(Raw.GraphExecChildGraphNodeSetParams(executable, node, replacement));
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize()); Require(calls == 3, "Replacement child execution.");
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(parent));
            if (replacement != 0) Check(Raw.GraphDestroy(replacement)); if (child != 0) Check(Raw.GraphDestroy(child)); GC.KeepAlive(callback);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void StreamCallback(IntPtr stream, HipError status, IntPtr userData);
    private static void Callbacks()
    {
        int hostCalls = 0, streamCalls = 0; IntPtr observedStream = 0; HipError observedStatus = HipError.InvalidValue;
        HostFunction host = data => { if (data == (IntPtr)21) Interlocked.Increment(ref hostCalls); };
        StreamCallback callback = (s, status, data) =>
        {
            observedStream = s; observedStatus = status; if (data == (IntPtr)22) Interlocked.Increment(ref streamCalls);
        };
        IntPtr stream = 0; Check(Raw.StreamCreate((IntPtr)(&stream)));
        try
        {
            Check(Raw.LaunchHostFunc(stream, Marshal.GetFunctionPointerForDelegate(host), (IntPtr)21));
            Check(Raw.StreamAddCallback(stream, Marshal.GetFunctionPointerForDelegate(callback), (IntPtr)22, 0));
            Check(Raw.StreamSynchronize(stream)); Check(Raw.StreamSynchronize(stream));
            Require(hostCalls == 1 && streamCalls == 1 && observedStream == stream && observedStatus == HipError.Success, "Native callback count/data/status.");
        }
        finally { Check(Raw.StreamDestroy(stream)); GC.KeepAlive(host); GC.KeepAlive(callback); }
        int mode = 1; Check(Raw.ThreadExchangeStreamCaptureMode((IntPtr)(&mode)));
        Require(mode >= 0 && mode <= 2, "Invalid old thread capture mode.");
        Check(Raw.ThreadExchangeStreamCaptureMode((IntPtr)(&mode))); Require(mode == 1, "Thread capture mode round trip.");
    }

    private static void UserObject()
    {
        int destroyed = 0; IntPtr observed = 0;
        HostFunction destroy = data => { observed = data; Interlocked.Increment(ref destroyed); };
        IntPtr resource = 0, graph = 0;
        Check(Raw.UserObjectCreate((IntPtr)(&resource), (IntPtr)321, Marshal.GetFunctionPointerForDelegate(destroy), 1, 1));
        try
        {
            Check(Raw.UserObjectRetain(resource, 1)); Check(Raw.UserObjectRelease(resource, 1)); Require(destroyed == 0, "Premature user destruction.");
            Check(Raw.GraphCreate((IntPtr)(&graph), 0));
            Check(Raw.GraphRetainUserObject(graph, resource, 1, 0));
            Check(Raw.GraphReleaseUserObject(graph, resource, 1)); Require(destroyed == 0, "Graph released caller's user reference.");
            Check(Raw.UserObjectRelease(resource, 1)); resource = 0;
            Require(SpinWait.SpinUntil(() => Volatile.Read(ref destroyed) == 1, 5000) && observed == (IntPtr)321, "User object destructor/data.");
        }
        finally
        {
            if (graph != 0) Check(Raw.GraphDestroy(graph)); if (resource != 0) Check(Raw.UserObjectRelease(resource, 1)); GC.KeepAlive(destroy);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct InstantiateParameters
    {
        public IntPtr ErrorNode;
        public ulong Flags;
        public int Result;
        public IntPtr Stream;
    }

    private static void GraphUpdate()
    {
        IntPtr original = 0, changed = 0, firstNode = 0, changedNode = 0, executable = 0, pointer = 0;
        Check(Raw.Malloc((IntPtr)(&pointer), 256));
        try
        {
            Check(Raw.GraphCreate((IntPtr)(&original), 0));
            MemsetParameters parameters = new() { Destination = pointer, ElementSize = 1, Width = 256, Height = 1, Value = 0x45 };
            Check(Raw.GraphAddMemsetNode((IntPtr)(&firstNode), original, IntPtr.Zero, 0, (IntPtr)(&parameters)));
            InstantiateParameters instantiate = new() { Result = -1 };
            Check(Raw.GraphInstantiateWithParams((IntPtr)(&executable), original, (IntPtr)(&instantiate)));
            Require(executable != 0 && instantiate.Result == 0 && instantiate.ErrorNode == 0, "Instantiate parameters result.");
            Check(Raw.GraphClone((IntPtr)(&changed), original));
            Check(Raw.GraphNodeFindInClone((IntPtr)(&changedNode), firstNode, changed));
            parameters.Value = 0x67; Check(Raw.GraphMemsetNodeSetParams(changedNode, (IntPtr)(&parameters)));
            IntPtr errorNode = 0; int result = -1;
            Check(Raw.GraphExecUpdate(executable, changed, (IntPtr)(&errorNode), (IntPtr)(&result)));
            Require(result == 0 && errorNode == 0, "Executable graph update result.");
            Check(Raw.GraphLaunch(executable, IntPtr.Zero)); Check(Raw.DeviceSynchronize()); VerifyFill(pointer, 1, 0x67);
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); if (changed != 0) Check(Raw.GraphDestroy(changed));
            if (original != 0) Check(Raw.GraphDestroy(original)); Check(Raw.Free(pointer));
        }
    }
}
