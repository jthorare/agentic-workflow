using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Testing.Prove;

/// <summary>The in-memory <see cref="IArtifactFiles"/>: the files it was given by absolute path, hashed as the disk adapter would, and every path it was asked for.</summary>
public sealed class InMemoryArtifactFiles : IArtifactFiles
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    private readonly List<(string Workspace, string Path)> _asked = [];

    /// <summary>The paths asked for, in order: workspace and path as given.</summary>
    public IReadOnlyList<(string Workspace, string Path)> Asked => _asked;

    /// <summary>Holds a file.</summary>
    /// <param name="workspace">The phase worktree.</param>
    /// <param name="path">The file's path in it (relative, or absolute inside it).</param>
    /// <param name="text">Its content.</param>
    /// <returns>The same instance.</returns>
    public InMemoryArtifactFiles With(string workspace, string path, string text)
    {
        _files[Full(workspace, path)] = Encoding.UTF8.GetBytes(text);
        return this;
    }

    /// <summary>The hash a file of this content has.</summary>
    /// <param name="text">The content.</param>
    /// <returns>'sha256:' and the hex digits.</returns>
    public static string HashOf(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <inheritdoc/>
    public ValueTask<Result<string?>> Sha256(string workspace, string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _asked.Add((workspace, path));
        var root = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Full(workspace, path);
        if (!full.StartsWith(root, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<string?>>(SindriErrors.InvalidInput.With($"{path} is outside the phase worktree"));
        }

        return ValueTask.FromResult(Results.Success(_files.TryGetValue(full, out var bytes) ? "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes)) : null));
    }

    private static string Full(string workspace, string path) => Path.GetFullPath(Path.Combine(Path.GetFullPath(workspace), path));
}
