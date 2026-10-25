using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IRunDiff"/> in memory: a worktree is a HEAD name and a dirty flag the test sets. Held to <see cref="RunDiffContract"/>.</summary>
public sealed class FakeRunDiff : IRunDiff
{
    private readonly Dictionary<string, (string Head, bool Dirty)> _worktrees = new(StringComparer.Ordinal);

    private readonly Dictionary<string, SortedSet<string>> _changed = new(StringComparer.Ordinal);

    /// <summary>Makes <paramref name="worktree"/> exist at <paramref name="head"/>, clean.</summary>
    /// <param name="worktree">The path.</param>
    /// <param name="head">Its HEAD.</param>
    public void Add(string worktree, string head = "c1")
    {
        _worktrees[worktree] = (head, false);
        _changed[worktree] = new SortedSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Leaves an uncommitted change in <paramref name="worktree"/>, in the given paths.</summary>
    /// <param name="worktree">The path.</param>
    /// <param name="paths">The changed paths, relative to the worktree; the fake keeps them as changed against any base.</param>
    public void Touch(string worktree, params string[] paths)
    {
        _worktrees[worktree] = (_worktrees[worktree].Head, true);
        _changed[worktree].UnionWith(paths);
    }

    /// <summary>Makes <paramref name="paths"/> differ between older commits and HEAD, without leaving anything uncommitted.</summary>
    /// <param name="worktree">The path.</param>
    /// <param name="paths">The paths that differ from any earlier commit.</param>
    public void Differ(string worktree, params string[] paths) => _changed[worktree].UnionWith(paths);

    /// <summary>Commits in <paramref name="worktree"/>: a new HEAD, clean.</summary>
    /// <param name="worktree">The path.</param>
    public void Advance(string worktree) => _worktrees[worktree] = (_worktrees[worktree].Head + "+", false);

    /// <inheritdoc/>
    public ValueTask<Result<string>> Head(string worktree, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>(_worktrees.TryGetValue(worktree, out var found) ? found.Head : Missing(worktree));

    /// <inheritdoc/>
    public ValueTask<Result<bool>> IsEmpty(string worktree, string startHead, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<bool>>(_worktrees.TryGetValue(worktree, out var found) ? !found.Dirty && string.Equals(found.Head, startHead, StringComparison.Ordinal) : Missing(worktree));

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> ChangedPaths(string worktree, string baseCommit, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_changed.TryGetValue(worktree, out var paths) ? Results.Success<IReadOnlyList<string>>([.. paths]) : Missing(worktree));

    private static Error Missing(string worktree) => WorkflowErrors.EffectFailed.With($"{worktree} is not a worktree");
}
