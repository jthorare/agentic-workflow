using Aw.Kernel;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// The hidden-scenario store (line spec §9.1): <c language="text">$AW_SINDRI_HOME/holdout/&lt;line&gt;/&lt;phase&gt;/</c>, outside every repo, worktree and run area, with <c language="text">set.json</c>,
/// <c language="text">seal.json</c> (both written by step.plan.holdout) and <c language="text">spent.json</c> (written here, the scenarios that no longer count as hidden). This port is
/// what the hold-out log and the proof rules need; writing a set belongs to the author Step. A line or phase key that is not one safe path segment is an invalid input;
/// a phase with no set is <c language="text">SND-BLOCK-020</c>; a file that can't be read or parsed, or the store not writable, is <c language="text">SND-BLOCK-021</c>.
/// </summary>
public interface IHoldoutStore
{
    /// <summary>The phase's set.</summary>
    /// <param name="line">The line instance's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The set.</returns>
    public ValueTask<Result<HoldoutSet>> Set(string line, string phase, CancellationToken cancellationToken);

    /// <summary>The phase's seal.</summary>
    /// <param name="line">The line instance's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The seal.</returns>
    public ValueTask<Result<HoldoutSeal>> Seal(string line, string phase, CancellationToken cancellationToken);

    /// <summary>The ids of the scenarios marked spent.</summary>
    /// <param name="line">The line instance's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The ids; empty when none was ever marked.</returns>
    public ValueTask<Result<IReadOnlySet<string>>> Spent(string line, string phase, CancellationToken cancellationToken);

    /// <summary>Marks scenarios spent; marks already held stay, and marking twice changes nothing.</summary>
    /// <param name="line">The line instance's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <param name="scenarios">The ids to mark; each must be a scenario of the set.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when written.</returns>
    public ValueTask<Result<bool>> MarkSpent(string line, string phase, IReadOnlyCollection<string> scenarios, CancellationToken cancellationToken);
}
