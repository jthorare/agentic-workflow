using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Tests.Images;

/// <summary>An <see cref="IImageReader"/> that touches no disk: every path reads as the same tiny PNG. For unit tests of what a question does with its images' names.</summary>
internal sealed class FixedImageReader : IImageReader
{
    public ValueTask<ImageRead> Read(IReadOnlyList<string> paths, string? root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var bytes = ImageBytes.Png(1, 1);
        return ValueTask.FromResult(new ImageRead
        {
            Images =
            [
                .. paths.Select(path => new ImageAttachment { Path = path, Sha256 = "00", Bytes = bytes, Width = 1, Height = 1, Format = ImageFormat.Png }),
            ],
        });
    }
}
