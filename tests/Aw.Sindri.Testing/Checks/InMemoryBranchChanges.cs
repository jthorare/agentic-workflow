using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Testing.Checks;

/// <summary>The in-memory <see cref="IBranchChanges"/>: a test gives the changes between two commits of a worktree; any other range is an error, as an unknown commit is. Held to <see cref="BranchChangesContract"/>.</summary>
public sealed class InMemoryBranchChanges : IBranchChanges
{
    private readonly Dictionary<(string Worktree, string Base, string Head), BranchChanges> _ranges = [];

    /// <summary>Makes the range answer with <paramref name="changes"/>.</summary>
    /// <param name="worktree">The worktree.</param>
    /// <param name="baseSha">The base.</param>
    /// <param name="head">The head.</param>
    /// <param name="changes">What the branch changed.</param>
    /// <returns>The same instance.</returns>
    public InMemoryBranchChanges Add(string worktree, string baseSha, string head, BranchChanges changes)
    {
        _ranges[(worktree, baseSha, head)] = changes;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<BranchChanges>> Between(string worktree, string baseSha, string head, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Result<BranchChanges>>(_ranges.TryGetValue((worktree, baseSha, head), out var found)
            ? found
            : SindriErrors.InvalidInput.With($"{baseSha}..{head} is not a range of {worktree}"));
    }
}
