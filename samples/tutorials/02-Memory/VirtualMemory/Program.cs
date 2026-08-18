using System;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            const ulong byteLength = 4096;
            using var runtime = new HipRuntime();
            runtime.Initialize();
            HipDevice device = runtime.GetCurrentDevice();

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
}
