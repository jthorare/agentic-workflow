using System.Security.Cryptography;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Infrastructure.Images;

/// <summary>
/// Reads image files from disk: each once into memory, hashed (sha256, lowercase hex) and sized from those same bytes. A directory,
/// a missing file or anything that isn't a PNG or JPEG is a problem, and so is a file whose real path (every symlink on the way
/// resolved) is not under the root's real path.
/// </summary>
internal sealed class FileImageReader : IImageReader
{
    /// <inheritdoc />
    public async ValueTask<ImageRead> Read(IReadOnlyList<string> paths, string? root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var images = new List<ImageAttachment>(paths.Count);
        try
        {
            var realRoot = root is null ? null : RealPath(root);
            foreach (var given in paths)
            {
                var path = root is null || Path.IsPathRooted(given) ? given : Path.GetFullPath(given, root);
                var (image, problem) = await ReadOne(path, realRoot, cancellationToken).ConfigureAwait(false);
                if (image is null)
                {
                    return new ImageRead { Problem = problem };
                }

                images.Add(image);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // A root that is a symlink loop or can't be examined: the paths under it can't be vouched for.
            return new ImageRead { Problem = $"images under {root ?? "(no root)"} can't be read: {ex.Message}" };
        }

        return new ImageRead { Images = images };
    }

    private static async ValueTask<(ImageAttachment? Image, string? Problem)> ReadOne(string path, string? realRoot, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            return (null, $"image {path} is not an absolute path");
        }

        if (Directory.Exists(path))
        {
            return (null, $"image {path} is a directory");
        }

        try
        {
            var real = RealPath(path);
            if (realRoot is not null && !Under(real, realRoot))
            {
                return (null, $"image {path} resolves to {real}, outside {realRoot}");
            }

            if (!File.Exists(real))
            {
                return (null, $"image {path} does not exist");
            }

            // .NET has no file type for a path, and a FIFO, device or socket (which blocks or never ends when read) reports size 0:
            // anything with no size is refused, as no PNG or JPEG is empty.
            if (new FileInfo(real).Length == 0)
            {
                return (null, $"image {path} is not a regular file or is empty");
            }

            var bytes = await File.ReadAllBytesAsync(real, cancellationToken).ConfigureAwait(false);
            return Attach(path, bytes) is { } image ? (image, null) : (null, $"image {path} is not a PNG or JPEG");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, $"image {path} can't be read: {ex.Message}");
        }
    }

    private static ImageAttachment? Attach(string path, byte[] bytes) =>
        ImageHeader.Read(bytes) is { } header
            ? new ImageAttachment
            {
                Path = Path.GetFullPath(path),
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                Bytes = bytes,
                Width = header.Width,
                Height = header.Height,
                Format = header.Format,
            }
            : null;

    private static bool Under(string real, string realRoot) =>
        real.StartsWith(realRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    // The path with every symlink on the way resolved, component by component (a link in a middle directory counts, not only a link as the file).
    private static string RealPath(string path, int depth = 0)
    {
        const int MaxLinks = 40;
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full) ?? "/";
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is null)
            {
                continue;
            }

            if (depth >= MaxLinks)
            {
                throw new IOException($"too many levels of symbolic links at {current}");
            }

            var target = Path.GetFullPath(info.LinkTarget, Path.GetDirectoryName(current) ?? current);
            current = RealPath(target, depth + 1);
        }

        return current;
    }
}
