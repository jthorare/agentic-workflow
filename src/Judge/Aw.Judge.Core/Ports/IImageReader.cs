using Aw.Judge.Core.Questions;

namespace Aw.Judge.Core.Ports;

/// <summary>Reads image files once, for a question that sends images (Core does no I/O).</summary>
public interface IImageReader
{
    /// <summary>Reads each file once: its bytes, sha256 and the size and format from its header.</summary>
    /// <param name="paths">Absolute paths, in the order the question lists them.</param>
    /// <param name="root">A directory every file's real path (symlinks resolved) must stay under, or null for no such limit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The images, or the first problem.</returns>
    public ValueTask<ImageRead> Read(IReadOnlyList<string> paths, string? root, CancellationToken cancellationToken);
}
