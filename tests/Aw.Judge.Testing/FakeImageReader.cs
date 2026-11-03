using Aw.Judge.Core.Images;
using System.Security.Cryptography;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Testing;

/// <summary>
/// The IImageReader fake: the "files" are an in-memory map of absolute path to bytes, read the way the real reader reads them (header
/// for the size, sha256 of the bytes, relative paths taken from the root, a path outside the root refused). Passes ImageReaderContract.
/// With no files, it reads nothing and every path is missing.
/// </summary>
/// <param name="files">Absolute path to file bytes; null for none.</param>
public sealed class FakeImageReader(IReadOnlyDictionary<string, byte[]>? files = null) : IImageReader
{
    private readonly List<IReadOnlyList<string>> _reads = [];

    /// <summary>Every list of paths asked for, in order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Reads => _reads;

    /// <inheritdoc />
    public ValueTask<ImageRead> Read(IReadOnlyList<string> paths, string? root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _reads.Add(paths);
        var directory = root?.TrimEnd('/');
        var images = new List<ImageAttachment>(paths.Count);
        foreach (var given in paths)
        {
            var path = directory is null || given.StartsWith('/') ? given : directory + "/" + given;
            if (directory is not null && !path.StartsWith(directory + "/", StringComparison.Ordinal))
            {
                return Problem($"image {given} resolves outside {root}");
            }

            if (files is null || !files.TryGetValue(path, out var bytes))
            {
                return Problem($"image {given} does not exist");
            }

            if (ImageHeader.Read(bytes) is not { } header)
            {
                return Problem($"image {given} is not a PNG or JPEG");
            }

            images.Add(new ImageAttachment
            {
                Path = path,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                Bytes = bytes,
                Width = header.Width,
                Height = header.Height,
                Format = header.Format,
            });
        }

        return ValueTask.FromResult(new ImageRead { Images = images });
    }

    private static ValueTask<ImageRead> Problem(string problem) => ValueTask.FromResult(new ImageRead { Problem = problem });
}
