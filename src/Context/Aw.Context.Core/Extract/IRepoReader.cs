using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>Reads a repo's files at a commit, never the working tree, so a build is the same on every machine (S-4). A missing repo or commit is SND-CONTEXT-048.</summary>
public interface IRepoReader
{
    /// <summary>The commit HEAD points at, which a build reads (never the working tree).</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The commit sha, or SND-CONTEXT-048 when the checkout can't be read.</returns>
    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken);

    /// <summary>Lists the tracked files at a commit.</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The paths from the repo root, ordinal order.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken);

    /// <summary>Lists the tracked files at a commit, the repo's deny globs not applied: the map build counts what its ignored roles drop with it (issue 1161), and never reads these paths. A reader that can't see the dropped paths answers like <see cref="ListFiles"/>.</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The paths from the repo root, ordinal order.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> ListTracked(ContextRepo repo, string commit, CancellationToken cancellationToken) => ListFiles(repo, commit, cancellationToken);

    /// <summary>Reads one file at a commit.</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="path">The path from the repo root.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text.</returns>
    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken);

    /// <summary>Lists what a repo changed from the merge base of two commits to the second (the diff shape). Commit contents are never read, only which paths changed and how.</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="range">The two commit shas (7 to 64 lower-case hex characters each; abbreviated ones are expanded in the answer).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The full shas, the merge base and the changed paths (a rename is a deletion plus an addition); SND-CONTEXT-110 when the clone doesn't hold a sha, 111 when the commits share no merge base, 048 when the checkout can't be read.</returns>
    public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken);
}
