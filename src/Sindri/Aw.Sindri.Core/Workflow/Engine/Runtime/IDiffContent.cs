using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a committed change added and removed, for the PR gates (PB spec §10.1): the added lines for the secret scan and the test-weakening check, the deleted paths for the scope check.</summary>
public interface IDiffContent
{
    /// <summary>The lines the commits from <paramref name="baseCommit"/> to the worktree's HEAD added.</summary>
    /// <param name="worktree">The worktree.</param>
    /// <param name="baseCommit">The commit to compare with.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each added line with its path (relative to the worktree, '/' separated) and its 1-based line number in the new file; an error when the worktree or the commit can't be read.</returns>
    public ValueTask<Result<IReadOnlyList<(string Path, int Line, string Text)>>> AddedLines(string worktree, string baseCommit, CancellationToken cancellationToken);

    /// <summary>The paths the commits from <paramref name="baseCommit"/> to the worktree's HEAD deleted.</summary>
    /// <param name="worktree">The worktree.</param>
    /// <param name="baseCommit">The commit to compare with.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The deleted paths in ordinal order; an error when the worktree or the commit can't be read.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> DeletedPaths(string worktree, string baseCommit, CancellationToken cancellationToken);
}
