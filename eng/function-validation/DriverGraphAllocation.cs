using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;
internal static unsafe partial class Program
{
    private static void DriverGraphAllocation()
    {
        IntPtr graph = 0, executable = 0, allocation = 0, free = 0, set = 0, host = 0, copy = 0;
        Check(Raw.GraphCreate((IntPtr)(&graph), 0));
        try
        {
            GraphAllocationParameters parameters = new() { Pool = new PoolProperties { AllocationType = 1, Location = new HipMemLocation(1, 0) }, Size = 256 };
            if (!Capability(Raw.GraphAddMemAllocNode((IntPtr)(&allocation), graph, 0, 0, (IntPtr)(&parameters)))) return;
            Require(parameters.Pointer != 0, "Graph allocation address.");
            GraphAllocationParameters query = default; Check(Raw.GraphMemAllocNodeGetParams(allocation, (IntPtr)(&query)));
            Require(query.Pointer == parameters.Pointer && query.Size == 256, "Graph allocation params.");
            Check(Raw.HostMalloc((IntPtr)(&host), 256, 0));
            MemsetParameters memset = new() { Destination = parameters.Pointer, ElementSize = 1, Width = 256, Height = 1, Value = 0x6B };
            Check(Raw.GraphAddMemsetNode((IntPtr)(&set), graph, (IntPtr)(&allocation), 1, (IntPtr)(&memset)));
            Check(Raw.GraphAddMemcpyNode1D((IntPtr)(&copy), graph, (IntPtr)(&set), 1, host, parameters.Pointer, 256, 2));
            Check(Raw.DrvGraphAddMemFreeNode((IntPtr)(&free), graph, (IntPtr)(&copy), 1, parameters.Pointer));
            IntPtr freed = 0; Check(Raw.GraphMemFreeNodeGetParams(free, (IntPtr)(&freed))); Require(freed == parameters.Pointer, "Graph free address.");
            Check(Raw.GraphInstantiateWithFlags((IntPtr)(&executable), graph, 0));
            Check(Raw.GraphLaunch(executable, 0)); Check(Raw.DeviceSynchronize()); Require(new ReadOnlySpan<byte>((void*)host, 256).IndexOfAnyExcept((byte)0x6B) == -1, "Graph allocation data.");
            ulong used = 0, reserved = 0;
            Check(Raw.DeviceGetGraphMemAttribute(0, 0, (IntPtr)(&used)));
            Check(Raw.DeviceGetGraphMemAttribute(0, 2, (IntPtr)(&reserved))); Require(reserved >= used, "Graph memory accounting.");
            ulong zero = 0; Check(Raw.DeviceSetGraphMemAttribute(0, 1, (IntPtr)(&zero)));
            Check(Raw.DeviceGraphMemTrim(0));
        }
        finally
        {
            if (executable != 0) Check(Raw.GraphExecDestroy(executable)); Check(Raw.GraphDestroy(graph)); if (host != 0) Check(Raw.HostFree(host));
        }
    }

}
