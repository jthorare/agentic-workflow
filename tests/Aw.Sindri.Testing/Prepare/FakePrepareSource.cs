using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Prepare;

/// <summary>An <see cref="IPrepareSource"/> over files a test put at commits; every read is kept.</summary>
public sealed class FakePrepareSource : IPrepareSource
{
    private readonly Dictionary<(string Commit, string Path), string> _files = [];
    private readonly List<(string RepoPath, string Commit, string Path)> _reads = [];

    /// <summary>Every read so far, in order.</summary>
    public IReadOnlyList<(string RepoPath, string Commit, string Path)> Reads => _reads;

    /// <summary>Puts a file at a commit.</summary>
    /// <param name="commit">The commit.</param>
    /// <param name="path">The path relative to the repo root.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>The same instance.</returns>
    public FakePrepareSource With(string commit, string path, string text)
    {
        _files[(commit, path)] = text;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<string?>> FileAtCommit(string repoPath, string commit, string path, CancellationToken cancellationToken)
    {
        _reads.Add((repoPath, commit, path));
        return ValueTask.FromResult(Results.Success(_files.TryGetValue((commit, path), out var text) ? text : null));
    }
}
