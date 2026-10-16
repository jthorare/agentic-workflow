using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>What changed in a worktree's directory, for the sweep's change trigger.</summary>
public interface IWorktreeFiles
{
    /// <summary>
    /// The newest modification time among a directory, its entries and their entries (the top two levels, shallow on purpose),
    /// leaving out any entry whose name is in <paramref name="skipNames"/> (build outputs, .git) and what is under it.
    /// </summary>
    /// <param name="directory">The worktree's directory.</param>
    /// <param name="skipNames">Entry names to skip, compared exactly.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The newest time, or an error when the directory can't be read (the caller then keeps the worktree).</returns>
    public ValueTask<Result<DateTimeOffset>> NewestChange(string directory, IReadOnlyCollection<string> skipNames, CancellationToken cancellationToken);
}
