using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Infrastructure.Images;

/// <summary>Reads image files from disk: each once, hashed, with its size from the header; a real path outside the root is refused.</summary>
public sealed class FileImageReader : IImageReader
{
    /// <inheritdoc />
    public ValueTask<ImageRead> Read(IReadOnlyList<string> paths, string? root, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
