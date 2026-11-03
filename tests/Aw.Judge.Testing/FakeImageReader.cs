using System.Security.Cryptography;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Testing;

/// <summary>
/// The IImageReader fake: the "files" are an in-memory map of absolute path to bytes, read the way the real reader reads them (header
/// for the size, sha256 of the bytes, relative paths taken from the root, '..' folded away, symlinks resolved, a path outside the
/// root refused, a directory refused). Passes ImageReaderContract. With no files, it reads nothing and every path is missing.
/// </summary>
/// <param name="files">Absolute path to file bytes; null for none.</param>
/// <param name="links">Absolute path of a symlink to the absolute path it points at (a file or a directory); null for none.</param>
public sealed class FakeImageReader(IReadOnlyDictionary<string, byte[]>? files = null, IReadOnlyDictionary<string, string>? links = null) : IImageReader
{
    private const int MaxLinks = 40;

    private readonly List<IReadOnlyList<string>> _reads = [];

    /// <summary>Every list of paths asked for, in order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Reads => _reads;

    /// <inheritdoc />
    public ValueTask<ImageRead> Read(IReadOnlyList<string> paths, string? root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _reads.Add(paths);
        var realRoot = root is null ? null : Real(root);
        var images = new List<ImageAttachment>(paths.Count);
        foreach (var given in paths)
        {
            var path = Folded(root is null || given.StartsWith('/') ? given : root.TrimEnd('/') + "/" + given);
            var (image, problem) = ReadOne(given, path, realRoot);
            if (image is null)
            {
                return Problem(problem);
            }

            images.Add(image);
        }

        return ValueTask.FromResult(new ImageRead { Images = images });
    }

    private (ImageAttachment? Image, string Problem) ReadOne(string given, string path, string? realRoot)
    {
        var real = Real(path);
        if (realRoot is not null && !real.StartsWith(realRoot.TrimEnd('/') + "/", StringComparison.Ordinal))
        {
            return (null, $"image {given} resolves to {real}, outside {realRoot}");
        }

        if (files is not null && files.Keys.Any(k => k.StartsWith(real + "/", StringComparison.Ordinal)))
        {
            return (null, $"image {given} is a directory");
        }

        if (files is null || !files.TryGetValue(real, out var bytes))
        {
            return (null, $"image {given} does not exist");
        }

        return ImageHeader.Read(bytes) is { } header
            ? (new ImageAttachment
            {
                Path = path,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                Bytes = bytes,
                Width = header.Width,
                Height = header.Height,
                Format = header.Format,
            }, "")
            : (null, $"image {given} is not a PNG or JPEG");
    }

    private static string Folded(string path) => Path.GetFullPath(path);

    // The path with every symlink on the way replaced by its target, as the file system would resolve it.
    private string Real(string path)
    {
        var current = Folded(path);
        for (var hops = 0; links is not null && hops <= MaxLinks; hops++)
        {
            var link = links.Keys.Where(l => current == l || current.StartsWith(l + "/", StringComparison.Ordinal)).OrderByDescending(l => l.Length).FirstOrDefault();
            if (link is null)
            {
                return current;
            }

            current = Folded(links[link] + current[link.Length..]);
        }

        return current;
    }

    private static ValueTask<ImageRead> Problem(string problem) => ValueTask.FromResult(new ImageRead { Problem = problem });
}
