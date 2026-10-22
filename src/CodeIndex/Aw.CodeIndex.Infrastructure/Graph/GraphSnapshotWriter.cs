using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Graph;

/// <summary>
/// Writes graphify's snapshot from the request's filtered inventory (spec §5.5, CI3): blobs from git objects, never the working tree, never a link,
/// never graphify's own folders; every write is asserted to resolve under the snapshot root. What the caps leave out is not an error.
/// </summary>
internal static class GraphSnapshotWriter
{
    private static readonly string[] _reserved = ["graphify-out", ".sindri-tmp"];

    /// <summary>Writes the files.</summary>
    /// <param name="request">The files and where their contents come from.</param>
    /// <param name="root">The snapshot folder (already made).</param>
    /// <param name="options">The caps.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>Success, or the blob reader's error.</returns>
    public static async Task<Result<Unit>> Write(GraphRequest request, string root, GraphifyOptions options, CancellationToken cancellationToken)
    {
        var anchor = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        long copied = 0;
        foreach (var file in request.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Safe(file.Path) || file.Size > options.MaxFileBytes || copied + file.Size > options.MaxSnapshotBytes)
            {
                continue;
            }

            var bytes = await request.Blobs.Read(file.Blob, cancellationToken).ConfigureAwait(false);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            if (bytes.Value is not { } content)
            {
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(root, file.Path));
            if (!target.StartsWith(anchor, StringComparison.Ordinal))
            {
                throw new InvalidDataException("a snapshot write would leave the snapshot root");
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);   // why: target is a file path under the root
            await File.WriteAllBytesAsync(target, content, cancellationToken).ConfigureAwait(false);
            copied += content.Length;
        }

        return Unit.Value;
    }

    private static bool Safe(string path)
    {
        var segments = path.Split('/');
        return path.Length > 0 && !path.Contains('\\', StringComparison.Ordinal) && !path.Contains('\0', StringComparison.Ordinal)
            && segments.All(s => s.Length > 0 && s is not ("." or "..") && !string.Equals(s, ".git", StringComparison.OrdinalIgnoreCase))
            && !_reserved.Any(r => segments[0].StartsWith(r, StringComparison.Ordinal));
    }
}
