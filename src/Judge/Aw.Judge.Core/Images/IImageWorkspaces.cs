
namespace Aw.Judge.Core.Images;

/// <summary>Makes the per-call directories the agent CLIs read images from (Core does no I/O).</summary>
public interface IImageWorkspaces
{
    /// <summary>Writes the images, as already read (never re-reading their paths), into a fresh private directory.</summary>
    /// <param name="images">The images, in order.</param>
    /// <param name="relativeTo">When set, each file keeps its path relative to this directory; else it is named image-1.png, image-2.jpg, ...</param>
    /// <returns>The workspace; the caller disposes it.</returns>
    /// <remarks>Throws (IOException, UnauthorizedAccessException) when the directory or a file can't be written; the agent CLI that asked reports it.</remarks>
    public IImageWorkspace Create(IReadOnlyList<ImageAttachment> images, string? relativeTo);
}
