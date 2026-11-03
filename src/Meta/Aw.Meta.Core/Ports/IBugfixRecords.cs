using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>What the bugfix-state runs under $AW_STATE_DIR/bugfix recorded, for the sweep's --discarded check.</summary>
public interface IBugfixRecords
{
    /// <summary>The candidates of every run that name a worktree.</summary>
    /// <param name="worktree">The worktree's directory.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The matching candidates (empty when none), or an error when the records can't be listed.</returns>
    public ValueTask<Result<IReadOnlyList<BugfixCandidate>>> Naming(string worktree, CancellationToken cancellationToken);
}
