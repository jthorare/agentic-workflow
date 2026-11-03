using System.Buffers.Binary;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// Reads an image's format and size from its first bytes, with no image library: a PNG by its magic bytes (89 50 4E 47) and
/// IHDR, a JPEG by its magic bytes (FF D8 FF) and first SOFn marker (C0-CF except C4, C8 and CC). Pure.
/// </summary>
public static class ImageHeader
{
    private const int PngHeaderLength = 24;

    /// <summary>Reads the header.</summary>
    /// <param name="bytes">The file's bytes (or at least its first segments).</param>
    /// <returns>The format, width and height, or null when the bytes are not a PNG or JPEG with a readable header.</returns>
    public static (ImageFormat Format, int Width, int Height)? Read(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && bytes[..4].SequenceEqual<byte>([0x89, 0x50, 0x4E, 0x47]) ? Png(bytes)
        : bytes.Length >= 3 && bytes[..3].SequenceEqual<byte>([0xFF, 0xD8, 0xFF]) ? Jpeg(bytes)
        : null;

    private static (ImageFormat, int, int)? Png(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < PngHeaderLength || !bytes[12..16].SequenceEqual<byte>([(byte)'I', (byte)'H', (byte)'D', (byte)'R']))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]);
        return Sized(ImageFormat.Png, width, height);
    }

    // Walks the marker segments to the first start-of-frame: [FF][marker][length hi][length lo][precision][height 2][width 2].
    private static (ImageFormat, int, int)? Jpeg(ReadOnlySpan<byte> bytes)
    {
        var i = 2;
        while (i + 1 < bytes.Length && bytes[i] == 0xFF)
        {
            var marker = bytes[i + 1];
            i += 2;
            if (marker == 0xFF)
            {
                i--; // fill byte: the next 0xFF starts the marker
            }
            else if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                return Frame(bytes, i);
            }
            else if (marker is not (0x01 or (>= 0xD0 and <= 0xD8)))
            {
                // A segment with a length: a marker byte of 0, the end of the image or the start of the scan before any frame ends the walk.
                if (marker is 0x00 or 0xD9 or 0xDA || i + 2 > bytes.Length || BinaryPrimitives.ReadUInt16BigEndian(bytes[i..]) < 2)
                {
                    return null;
                }

                i += BinaryPrimitives.ReadUInt16BigEndian(bytes[i..]);
            }
        }

        return null;
    }

    // At the segment's length field: [length 2][precision][height 2][width 2].
    private static (ImageFormat, int, int)? Frame(ReadOnlySpan<byte> bytes, int at) =>
        at + 7 > bytes.Length ? null : Sized(ImageFormat.Jpeg, BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 5)..]), BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 3)..]));

    private static (ImageFormat, int, int)? Sized(ImageFormat format, uint width, uint height) =>
        width is > 0 and <= int.MaxValue && height is > 0 and <= int.MaxValue ? (format, (int)width, (int)height) : null;
}
