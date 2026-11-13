using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static unsafe int Main(string[] args)
    {
        try
        {
            using var runtime = new HipRuntime();
            runtime.Initialize();
            HipDevice device = runtime.GetCurrentDevice();
            if (runtime.GetDeviceAttribute(HipDeviceAttribute.VirtualMemoryManagementSupported, device.Ordinal) == 0)
            {
                Console.WriteLine("Skipped: device reports no virtual-memory management support.");
                return 0;
            }
            // Query the device requirement rather than assuming a 4 KB page / 查询设备粒度，不假设 4 KB 页大小。
            var properties = new AllocationProperties { Type = 1, LocationType = 1, Device = device.Ordinal };
            UIntPtr granularity = UIntPtr.Zero;
            HipError error = new HipRuntimeNativeApi().MemGetAllocationGranularity((IntPtr)(&granularity), (IntPtr)(&properties), 0);
            if (error == HipError.NotSupported)
            {
                Console.WriteLine("Skipped: allocation granularity query is not supported by this HIP Runtime.");
                return 0;
            }
            if (error != HipError.Success)
            {
                throw new InvalidOperationException("hipMemGetAllocationGranularity failed: " + error);
            }
            ulong byteLength = granularity.ToUInt64();
            if (byteLength == 0)
            {
                throw new InvalidOperationException("HIP returned a zero allocation granularity.");
            }

            using (HipVirtualMemoryReservation reservation = runtime.ReserveVirtualMemory(byteLength))
            using (HipPhysicalMemoryAllocation allocation = runtime.CreatePhysicalMemory(byteLength, new HipVirtualMemoryAllocationOptions(device.Ordinal)))
            using (HipVirtualMemoryMapping mapping = reservation.Map(allocation, byteLength))
            {
                var location = new HipMemLocation(1, device.Ordinal);
                reservation.SetAccess(byteLength, new HipVirtualMemoryAccessDescriptor(location, HipMemoryAccessFlags.ReadWrite));
                bool passed = reservation.GetAccess(location) == HipMemoryAccessFlags.ReadWrite;
                Console.WriteLine(passed ? "Virtual memory reserve/map/access passed." : "Virtual memory access verification failed.");
                return passed ? 0 : 1;
            }
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: virtual memory is not supported by this HIP Runtime or device.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    // hipMemAllocationProp layout, with default flags / hipMemAllocationProp 布局，所有标志默认为零。
    [StructLayout(LayoutKind.Sequential)]
    private struct AllocationProperties
    {
        internal int Type;
        internal int HandleTypes;
        internal int LocationType;
        internal int Device;
        internal IntPtr WindowsMetadata;
        internal uint Flags;
    }
}
