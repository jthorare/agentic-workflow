using System.Buffers.Binary;
using System.Text;

namespace Aw.Judge.Tests.Images;

/// <summary>Minimal image files for the tests: a real header with the size in it, then a tag so two files differ.</summary>
internal static class ImageBytes
{
    /// <summary>A PNG signature and IHDR chunk (no image data; the judge reads the header only).</summary>
    public static byte[] Png(int width, int height, string tag = "")
    {
        var header = new byte[8 + 4 + 4 + 13 + 4];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(header, 12);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20), (uint)height);
        header[24] = 8; // bit depth
        header[25] = 2; // RGB
        return [.. header, .. Encoding.ASCII.GetBytes(tag)];
    }

    /// <summary>A JPEG with an APP0 segment, a DHT segment (marker C4, not a frame) and then an SOF0 frame header.</summary>
    public static byte[] Jpeg(int width, int height, string tag = "")
    {
        var sof = new byte[19];
        sof[0] = 0xFF;
        sof[1] = 0xC0;
        BinaryPrimitives.WriteUInt16BigEndian(sof.AsSpan(2), 17);
        sof[4] = 8; // precision
        BinaryPrimitives.WriteUInt16BigEndian(sof.AsSpan(5), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(sof.AsSpan(7), (ushort)width);
        sof[9] = 3; // components
        byte[] app0 = [0xFF, 0xE0, 0x00, 0x10, .. Encoding.ASCII.GetBytes("JFIF\0"), 1, 1, 0, 0, 1, 0, 1, 0, 0];
        byte[] dht = [0xFF, 0xC4, 0x00, 0x05, 0x00, 0x01, 0x02];
        return [0xFF, 0xD8, .. app0, .. dht, .. sof, .. Encoding.ASCII.GetBytes(tag)];
    }

    /// <summary>A GIF header: an image, but not one the judge takes.</summary>
    public static byte[] Gif() => [.. Encoding.ASCII.GetBytes("GIF89a"), 0x10, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00];
}
