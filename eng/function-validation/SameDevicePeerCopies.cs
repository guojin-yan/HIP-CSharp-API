using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PeerCopy3D
    {
        public IntPtr SourceArray;
        public Position SourcePosition;
        public HipPitchedPtr Source;
        public int SourceDevice;
        public IntPtr DestinationArray;
        public Position DestinationPosition;
        public HipPitchedPtr Destination;
        public int DestinationDevice;
        public HipExtent Extent;
    }

    // Exercise these entry points on one device. This does not prove cross-device P2P.
    private static void SameDevicePeerCopies()
    {
        LoadHelperPointer("ValidationKernel"); // Header static_assert validates PeerCopy3D.
        Require(sizeof(PeerCopy3D) == 168, "Peer copy descriptor size.");
        IntPtr source = 0, destination = 0, stream = 0;
        int device = -1; Check(Raw.GetDevice((IntPtr)(&device)));
        Check(Raw.Malloc((IntPtr)(&source), 1024));
        try
        {
            Check(Raw.Malloc((IntPtr)(&destination), 1024));
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            byte* input = stackalloc byte[1024];
            byte* actual = stackalloc byte[1024];
            for (int i = 0; i < 1024; i++) input[i] = (byte)(i * 37 + 11);
            for (int variant = 0; variant < 4; variant++)
            {
                // Use position-dependent bytes so strides and offsets cannot pass accidentally.
                Check(Raw.MemcpyHtoD(source, (IntPtr)input, 1024));
                Check(Raw.Memset(destination, 0xCD, 1024));
                if (variant == 0) Check(Raw.MemcpyPeer(destination, device, source, device, 256));
                else if (variant == 1) Check(Raw.MemcpyPeerAsync(destination, device, source, device, 256, stream));
                else
                {
                    PeerCopy3D copy = new()
                    {
                        Source = new HipPitchedPtr(source, 64, 64, 4), SourceDevice = device,
                        Destination = new HipPitchedPtr(destination, 64, 64, 4), DestinationDevice = device,
                        SourcePosition = new Position { X = 5 }, DestinationPosition = new Position { X = 9 },
                        Extent = new HipExtent(32, 4, 2)
                    };
                    if (variant == 2) Check(Raw.Memcpy3DPeer((IntPtr)(&copy)));
                    else Check(Raw.Memcpy3DPeerAsync((IntPtr)(&copy), stream));
                }
                Check(Raw.StreamSynchronize(stream));
                Check(Raw.MemcpyDtoH((IntPtr)actual, destination, 1024));
                for (int i = 0; i < 1024; i++)
                {
                    bool copied = variant < 2 ? i < 256 : i < 512 && i % 64 >= 9 && i % 64 < 41;
                    byte expected = copied ? input[variant < 2 ? i : i - 4] : (byte)0xCD;
                    Require(actual[i] == expected, $"Same-device peer variant {variant}, byte {i}.");
                }
            }
        }
        finally
        {
            if (stream != 0) Check(Raw.StreamDestroy(stream));
            if (destination != 0) Check(Raw.Free(destination));
            Check(Raw.Free(source));
        }
    }
}
