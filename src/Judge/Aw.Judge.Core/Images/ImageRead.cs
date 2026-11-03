namespace Aw.Judge.Core.Images;

/// <summary>What IImageReader found: the images in the order asked for, or the first problem in words.</summary>
public sealed record ImageRead
{
    /// <summary>The images, in the order of the paths; empty when <see cref="Problem"/> is set.</summary>
    public IReadOnlyList<ImageAttachment> Images { get; init; } = [];

    /// <summary>
    /// Why the paths can't be read (a missing file, a directory, not a PNG or JPEG, a symlink whose real path leaves the root),
    /// naming the path; null when every path was read.
    /// </summary>
    public string? Problem { get; init; }
}
