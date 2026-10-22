using System.Security.Cryptography;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Infrastructure.Prove;

/// <summary><see cref="IArtifactFiles"/> on disk: a file is hashed in place, and a path that leaves the worktree (by '..', an absolute path or a link) is refused before it is opened.</summary>
internal sealed class DiskArtifactFiles : IArtifactFiles
{
    /// <inheritdoc/>
    public async ValueTask<Result<string?>> Sha256(string workspace, string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = Path.GetFullPath(workspace);
        var file = Path.GetFullPath(Path.Combine(root, path));
        if (!Inside(root, file))
        {
            return Outside(path);
        }

        if (!File.Exists(file))
        {
            return Results.Success<string?>(value: null);
        }

        try
        {
            if (new FileInfo(file).ResolveLinkTarget(returnFinalTarget: true) is { } target && !Inside(Path.GetFullPath(new DirectoryInfo(root).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? root), target.FullName))
            {
                return Outside(path);
            }

            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
            await using var _ = stream.ConfigureAwait(false);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return Results.Success<string?>("sha256:" + Convert.ToHexStringLower(hash));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.InvalidInput.With($"can't read {path}: {e.Message}");
        }
    }

    private static bool Inside(string root, string file) =>
        file.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static Result<string?> Outside(string path) => SindriErrors.InvalidInput.With($"{path} is outside the phase worktree");
}
