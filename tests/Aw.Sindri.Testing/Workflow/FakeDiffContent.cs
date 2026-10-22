using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IDiffContent"/> in memory: the added lines and deleted paths a test gave for a worktree, whatever the base; a worktree it was not told about is an error.</summary>
public sealed class FakeDiffContent : IDiffContent
{
    private readonly Dictionary<string, List<(string Path, int Line, string Text)>> _added = new(StringComparer.Ordinal);

    private readonly Dictionary<string, SortedSet<string>> _deleted = new(StringComparer.Ordinal);

    /// <summary>The calls made, as 'worktree base'.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Makes <paramref name="worktree"/> exist with no change.</summary>
    /// <param name="worktree">The path.</param>
    /// <returns>The same instance.</returns>
    public FakeDiffContent Add(string worktree)
    {
        _ = _added.TryAdd(worktree, []);
        _ = _deleted.TryAdd(worktree, new SortedSet<string>(StringComparer.Ordinal));
        return this;
    }

    /// <summary>Adds lines to a file of <paramref name="worktree"/>, numbered from 1.</summary>
    /// <param name="worktree">The path.</param>
    /// <param name="path">The file.</param>
    /// <param name="lines">The added lines.</param>
    /// <returns>The same instance.</returns>
    public FakeDiffContent Adding(string worktree, string path, params string[] lines)
    {
        _ = Add(worktree);
        _added[worktree].AddRange(lines.Select((text, i) => (path, i + 1, text)));
        return this;
    }

    /// <summary>Deletes paths in <paramref name="worktree"/>.</summary>
    /// <param name="worktree">The path.</param>
    /// <param name="paths">The deleted paths.</param>
    /// <returns>The same instance.</returns>
    public FakeDiffContent Deleting(string worktree, params string[] paths)
    {
        _ = Add(worktree);
        _deleted[worktree].UnionWith(paths);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<(string Path, int Line, string Text)>>> AddedLines(string worktree, string baseCommit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add($"{worktree} {baseCommit}");
        return ValueTask.FromResult<Result<IReadOnlyList<(string Path, int Line, string Text)>>>(_added.TryGetValue(worktree, out var lines)
            ? Results.Success<IReadOnlyList<(string Path, int Line, string Text)>>([.. lines])
            : Missing(worktree));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> DeletedPaths(string worktree, string baseCommit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add($"{worktree} {baseCommit}");
        return ValueTask.FromResult<Result<IReadOnlyList<string>>>(_deleted.TryGetValue(worktree, out var paths)
            ? Results.Success<IReadOnlyList<string>>([.. paths])
            : Missing(worktree));
    }

    private static Error Missing(string worktree) => WorkflowErrors.EffectFailed.With($"{worktree} is not a worktree");
}
