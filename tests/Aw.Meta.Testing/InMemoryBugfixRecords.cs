using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IBugfixRecords: candidates added by hand, matched on the normalized worktree path.</summary>
public sealed class InMemoryBugfixRecords : IBugfixRecords
{
    private readonly List<BugfixCandidate> _rows = [];

    /// <summary>When set, every call fails with this error.</summary>
    public Error? Failure { get; set; }

    /// <summary>Records a candidate.</summary>
    /// <param name="candidate">The candidate.</param>
    public void Add(BugfixCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        _rows.Add(candidate);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<BugfixCandidate>>> Naming(string worktree, CancellationToken cancellationToken) =>
        Failure is { } error
            ? Done.Failed<IReadOnlyList<BugfixCandidate>>(error)
            : Done.Of<IReadOnlyList<BugfixCandidate>>([.. _rows.Where(r => string.Equals(WorktreeConfig.NormalizePath(r.Worktree), WorktreeConfig.NormalizePath(worktree), StringComparison.Ordinal))]);
}
