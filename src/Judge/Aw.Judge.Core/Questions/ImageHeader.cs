namespace Aw.Judge.Core.Questions;

/// <summary>
/// Reads an image's format and size from its first bytes, with no image library: a PNG by its magic bytes (89 50 4E 47) and
/// IHDR, a JPEG by its magic bytes (FF D8 FF) and first SOFn marker (C0-CF except C4, C8 and CC). Pure.
/// </summary>
public static class ImageHeader
{
    /// <summary>Reads the header.</summary>
    /// <param name="bytes">The file's bytes (or at least its first segments).</param>
    /// <returns>The format, width and height, or null when the bytes are not a PNG or JPEG with a readable header.</returns>
    public static (ImageFormat Format, int Width, int Height)? Read(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();
}
