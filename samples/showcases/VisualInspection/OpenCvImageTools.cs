using System;
using System.IO;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgCodecs;
using JYPPX.OpenCvSharp.ImgProc;
using CoreCv2 = JYPPX.OpenCvSharp.Core.Cv2;
using ImgCodecsCv2 = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using ImgProcCv2 = JYPPX.OpenCvSharp.ImgProc.Cv2;

internal static class OpenCvImageTools
{
    internal static Mat ReadGrayscale(string path)
    {
        Mat image = ImgCodecsCv2.ImRead(path, ImreadModes.Grayscale);
        if (image.Empty || image.Type != MatType.CV_8UC1)
        {
            image.Dispose();
            throw new InvalidDataException("OpenCV could not load an 8-bit grayscale image: " + path);
        }

        return image;
    }

    internal static PgmImage ToPgm(Mat image)
    {
        return PgmImage.FromPixels(image.Width, image.Height, image.ToArray<byte>());
    }

    internal static byte[] SegmentWithOpenCv(Mat image, int darkThreshold, int brightThreshold)
    {
        using var dark = new Mat();
        using var bright = new Mat();
        using var combined = new Mat();
        ImgProcCv2.Threshold(image, dark, darkThreshold - 1, 255, ThresholdTypes.BinaryInv);
        ImgProcCv2.Threshold(image, bright, brightThreshold, 255, ThresholdTypes.Binary);
        CoreCv2.BitwiseOr(dark, bright, combined);
        return combined.ToArray<byte>();
    }

    internal static void WritePng(string path, int width, int height, byte[] pixels)
    {
        using var image = new Mat(height, width, MatType.CV_8UC1);
        image.CopyFrom(pixels);
        if (!ImgCodecsCv2.ImWrite(path, image))
        {
            throw new IOException("OpenCV failed to write image: " + path);
        }
    }
}
