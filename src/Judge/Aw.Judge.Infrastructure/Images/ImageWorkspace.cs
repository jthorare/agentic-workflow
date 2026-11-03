using Aw.Judge.Core.Questions;

namespace Aw.Judge.Infrastructure.Images;

/// <summary>
/// A per-call directory ($TMPDIR/aw-judge-images-&lt;random&gt;/, mode 0700) holding the bytes that were read, for the agent CLIs: the
/// same bytes the other providers saw. Deleted on dispose.
/// </summary>
public sealed class ImageWorkspace : IDisposable
{
    /// <summary>The directory.</summary>
    public string Path { get; init; } = "";

    /// <summary>Writes the images into a fresh directory.</summary>
    /// <param name="images">The images, in order.</param>
    /// <param name="relativeTo">When set, each file lands under its path relative to this directory; else as image-1.png, image-2.jpg, ...</param>
    /// <returns>The workspace.</returns>
    public static ImageWorkspace Create(IReadOnlyList<ImageAttachment> images, string? relativeTo) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
