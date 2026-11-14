using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

// Linux KFD only. Linked into an external PackageReference-only consumer.
internal static unsafe class Program
{
    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int dup2(int oldFd, int newFd);

    private static void Check(HipError result, string api)
    {
        if (result != HipError.Success) throw new InvalidOperationException(api + ": " + result);
    }

    private static int Main(string[] args)
    {
        using var runtime = new HipRuntime();
        runtime.Initialize();
        var raw = new HipRuntimeNativeApi();
        var properties = new Properties { Type = 1, HandleTypes = 1, LocationType = 1, Device = runtime.GetCurrentDevice().Ordinal };
        UIntPtr granularity = UIntPtr.Zero;
        Check(raw.MemGetAllocationGranularity((IntPtr)(&granularity), (IntPtr)(&properties), 0), "hipMemGetAllocationGranularity");
        ulong length = granularity.ToUInt64();
        using var allocation = runtime.CreatePhysicalMemory(length, new HipVirtualMemoryAllocationOptions(properties.Device) { RequestedHandleTypes = HipMemoryAllocationHandleType.PosixFileDescriptor });
        int fd = allocation.ExportShareableHandle(HipMemoryAllocationHandleType.PosixFileDescriptor).ToInt32();
        if (fd < 0) throw new InvalidOperationException("Invalid exported descriptor.");
        try
        {
            using var first = runtime.ReserveVirtualMemory(length);
            using var originalMapping = first.Map(allocation, length);
            var access = new HipVirtualMemoryAccessDescriptor(new HipMemLocation(1, properties.Device), HipMemoryAccessFlags.ReadWrite);
            first.SetAccess(length, access);
            int input = 1234567, output = 0;
            Check(raw.Memcpy(first.DangerousGetAddress(), (IntPtr)(&input), (UIntPtr)sizeof(int), 1), "hipMemcpy(original)");
            using (var imported = runtime.ImportPhysicalMemory(new IntPtr(fd), HipMemoryAllocationHandleType.PosixFileDescriptor, length))
            using (var second = runtime.ReserveVirtualMemory(length))
            using (var importedMapping = second.Map(imported, length))
            {
                second.SetAccess(length, access);
                Check(raw.Memcpy((IntPtr)(&output), second.DangerousGetAddress(), (UIntPtr)sizeof(int), 2), "hipMemcpy(imported)");
                if (output != input) throw new InvalidOperationException("Shared mapping bytes differ.");
            }
            output = 0;
            Check(raw.Memcpy((IntPtr)(&output), first.DangerousGetAddress(), (UIntPtr)sizeof(int), 2), "hipMemcpy(original-after-import-release)");
            if (output != input) throw new InvalidOperationException("Original owner lost its allocation.");
            if (dup2(fd, 0) != 0) throw new InvalidOperationException("dup2 failed: " + Marshal.GetLastWin32Error());
            try
            {
                using var zero = runtime.ImportPhysicalMemory(IntPtr.Zero, HipMemoryAllocationHandleType.PosixFileDescriptor, length);
                Console.WriteLine("POSIX_FD_ZERO=native-accepted");
            }
            catch (HipException exception) when (exception.Error == HipError.InvalidValue)
            {
                // Observed native HIP restriction; the managed layer must preserve its error.
                Console.WriteLine("POSIX_FD_ZERO=native-InvalidValue-propagated");
            }
            finally
            {
                if (fd != 0 && close(0) != 0) throw new InvalidOperationException("close(0) failed.");
            }
            if (fd != 0)
            {
                int exportedZero = allocation.ExportShareableHandle(HipMemoryAllocationHandleType.PosixFileDescriptor).ToInt32();
                try
                {
                    if (exportedZero != 0) throw new InvalidOperationException("Expected native export to reuse the vacant descriptor zero.");
                    Console.WriteLine("POSIX_EXPORT_ZERO=accepted");
                }
                finally
                {
                    if (close(exportedZero) != 0) throw new InvalidOperationException("close(second export) failed.");
                }
            }
        }
        finally
        {
            if (close(fd) != 0) throw new InvalidOperationException("close(exported descriptor) failed.");
        }
        Console.WriteLine("PHYSICAL_SHARING=passed; sharedBytes=true; independentImportRelease=true; originalOwnerAlive=true");
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Properties
    {
        internal int Type, HandleTypes, LocationType, Device;
        internal IntPtr WindowsMetadata;
        internal uint Flags;
    }
}
