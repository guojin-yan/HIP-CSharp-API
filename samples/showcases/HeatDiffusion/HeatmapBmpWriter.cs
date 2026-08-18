using System;
using System.IO;

internal static class HeatmapBmpWriter
{
    internal static void Write(string path, float[] field, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(field);

        if (field.Length != checked(width * height))
        {
            throw new ArgumentException("The heat field does not match the image dimensions.", nameof(field));
        }

        int rowBytes = checked(width * 3);
        int rowStride = checked((rowBytes + 3) & ~3);
        int pixelBytes = checked(rowStride * height);
        int fileBytes = checked(54 + pixelBytes);

        using FileStream stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileBytes);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixelBytes);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        int padding = rowStride - rowBytes;
        for (int y = height - 1; y >= 0; y--)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                (byte red, byte green, byte blue) = MapColor(field[row + x]);
                writer.Write(blue);
                writer.Write(green);
                writer.Write(red);
            }

            for (int index = 0; index < padding; index++)
            {
                writer.Write((byte)0);
            }
        }
    }

    private static (byte Red, byte Green, byte Blue) MapColor(float temperature)
    {
        float normalized = Math.Clamp((temperature - 5.0f) / 95.0f, 0.0f, 1.0f);
        float red = Math.Clamp(1.5f - Math.Abs((4.0f * normalized) - 3.0f), 0.0f, 1.0f);
        float green = Math.Clamp(1.5f - Math.Abs((4.0f * normalized) - 2.0f), 0.0f, 1.0f);
        float blue = Math.Clamp(1.5f - Math.Abs((4.0f * normalized) - 1.0f), 0.0f, 1.0f);
        return ((byte)(red * 255.0f), (byte)(green * 255.0f), (byte)(blue * 255.0f));
    }
}
