namespace Aw.Judge.Core.Images;

/// <summary>
/// One image read once from disk: the bytes every provider gets (so a file that changes mid-call can't give two providers two
/// pictures), their hash and the size and format read from the header.
/// </summary>
public sealed record ImageAttachment
{
    /// <summary>The absolute path it was read from.</summary>
    public required string Path { get; init; }

    /// <summary>The sha256 of <see cref="Bytes"/>, lowercase hex.</summary>
    public required string Sha256 { get; init; }

    /// <summary>The file's bytes.</summary>
    public required ReadOnlyMemory<byte> Bytes { get; init; }

    /// <summary>The width in pixels, from the header.</summary>
    public required int Width { get; init; }

    /// <summary>The height in pixels, from the header.</summary>
    public required int Height { get; init; }

    /// <summary>The format, from the magic bytes.</summary>
    public required ImageFormat Format { get; init; }
}
