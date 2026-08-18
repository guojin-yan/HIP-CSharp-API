using System;
using System.IO;
using System.Text;

internal sealed class PgmImage
{
    private PgmImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal byte[] Pixels { get; }

    internal static PgmImage FromPixels(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height))
        {
            throw new InvalidDataException("Image dimensions do not match the pixel payload.");
        }

        return new PgmImage(width, height, pixels);
    }

    internal static PgmImage Read(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int offset = 0;
        string magic = ReadToken(data, ref offset);
        if (!string.Equals(magic, "P5", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Only binary PGM (P5) files are supported: " + path);
        }

        int width = ParsePositive(ReadToken(data, ref offset), "width");
        int height = ParsePositive(ReadToken(data, ref offset), "height");
        int maximum = int.Parse(ReadToken(data, ref offset), System.Globalization.CultureInfo.InvariantCulture);
        if (maximum != 255)
        {
            throw new InvalidDataException("Only 8-bit PGM files are supported: " + path);
        }

        while (offset < data.Length && data[offset] is 9 or 10 or 13 or 32)
        {
            offset++;
        }

        int length = checked(width * height);
        if (data.Length - offset != length)
        {
            throw new InvalidDataException("PGM payload length does not match its dimensions: " + path);
        }

        var pixels = new byte[length];
        Buffer.BlockCopy(data, offset, pixels, 0, length);
        return new PgmImage(width, height, pixels);
    }

    internal void Write(string path)
    {
        using FileStream stream = File.Create(path);
        byte[] header = Encoding.ASCII.GetBytes($"P5\n{Width} {Height}\n255\n");
        stream.Write(header, 0, header.Length);
        stream.Write(Pixels, 0, Pixels.Length);
    }

    private static string ReadToken(byte[] data, ref int offset)
    {
        while (offset < data.Length)
        {
            if (data[offset] == (byte)'#')
            {
                while (offset < data.Length && data[offset] != (byte)'\n')
                {
                    offset++;
                }

                continue;
            }

            if (data[offset] is 9 or 10 or 13 or 32)
            {
                offset++;
                continue;
            }

            break;
        }

        int start = offset;
        while (offset < data.Length && data[offset] is not (9 or 10 or 13 or 32))
        {
            offset++;
        }

        if (start == offset)
        {
            throw new InvalidDataException("PGM header ended before all fields were found.");
        }

        return Encoding.ASCII.GetString(data, start, offset - start);
    }

    private static int ParsePositive(string value, string name)
    {
        if (!int.TryParse(value, out int result) || result <= 0)
        {
            throw new InvalidDataException("PGM " + name + " must be positive.");
        }

        return result;
    }
}
