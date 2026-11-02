using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>Reads a file as a commit has it, never from a worktree (sandbox spec §3.8: the prepare's cache key comes from the run's base commit).</summary>
public interface IPrepareSource
{
    /// <summary>The text of <paramref name="path"/> at <paramref name="commit"/> (<c lang="text">git show &lt;commit&gt;:&lt;path&gt;</c>).</summary>
    /// <param name="repoPath">The repo checkout.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="path">The path relative to the repo root, with '/'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, or null when the commit has no such file.</returns>
    public ValueTask<Result<string?>> FileAtCommit(string repoPath, string commit, string path, CancellationToken cancellationToken);
}
