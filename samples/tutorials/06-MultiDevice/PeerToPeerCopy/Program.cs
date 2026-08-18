using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Peer;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            using var runtime = new HipRuntime();
            runtime.Initialize();
            IReadOnlyList<HipDevice> devices = runtime.GetDevices();
            if (devices.Count < 2)
            {
                Console.WriteLine("Skipped: at least two HIP devices are required.");
                return 0;
            }

            const int accessingDevice = 0;
            const int peerDevice = 1;
            if (!runtime.CanAccessPeer(accessingDevice, peerDevice))
            {
                Console.WriteLine("Skipped: device 0 cannot access device 1.");
                return 0;
            }

            byte[] expected = { 17, 34, 51, 68 };
            HipDeviceMemory? source = null;
            try
            {
                devices[peerDevice].MakeCurrent();
                source = runtime.Allocate((ulong)expected.Length);
                source.CopyFrom(expected);

                devices[accessingDevice].MakeCurrent();
                using HipDeviceMemory destination = runtime.Allocate((ulong)expected.Length);
                using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
                using HipPeerAccess access = runtime.EnablePeerAccess(accessingDevice, peerDevice);
                access.CopyAsync(destination, source, (ulong)expected.Length, stream);
                stream.Synchronize();

                var actual = new byte[expected.Length];
                destination.CopyTo(actual);
                bool passed = expected.SequenceEqual(actual);
                Console.WriteLine(passed ? "Peer-to-peer copy passed." : "Peer-to-peer copy failed.");
                return passed ? 0 : 1;
            }
            finally
            {
                if (source is not null)
                {
                    devices[peerDevice].MakeCurrent();
                    source.Dispose();
                }

                devices[accessingDevice].MakeCurrent();
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
