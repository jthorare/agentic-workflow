using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary><see cref="IPrepareSource"/> over <c lang="text">git show &lt;commit&gt;:&lt;path&gt;</c>: the lockfile as the base commit has it, never the worktree's.</summary>
internal sealed class GitPrepareSource : IPrepareSource
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public async ValueTask<Result<string?>> FileAtCommit(string repoPath, string commit, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repoPath);
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(path);
        // why: a ':' or leading '-' in the commit would change what git reads; the commit is a sha the launcher recorded, and "--end-of-options" keeps it from being a flag
        var shown = await ViewerProcess.Output("git", ["-C", repoPath, "show", "--end-of-options", commit + ":" + path], _budget, cancellationToken).ConfigureAwait(false);
        return Results.Success(shown);   // null: git failed or the commit has no such file, which the prepare reports as lockfile-missing
    }
}
