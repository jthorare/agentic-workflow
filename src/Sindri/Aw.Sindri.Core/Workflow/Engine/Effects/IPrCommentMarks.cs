using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// The comment marks of the PR babysitter (port; spec §5.4, W26): 'pr_comment_marks' in ledger.db. Rows are append-only;
/// what a comment's state is, is the latest row per (comment, edited-at).
/// </summary>
public interface IPrCommentMarks
{
    /// <summary>Appends <paramref name="mark"/>, unless a mark with the same effect identity is already recorded (a re-delivered effect).</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the row was written now; false when its effect identity was recorded before.</returns>
    public ValueTask<Result<bool>> Write(PrCommentMark mark, CancellationToken cancellationToken);

    /// <summary>The state of every marked comment of <paramref name="pr"/>: the latest row per (comment, edited-at) by time, the later write on a tie.</summary>
    /// <param name="pr">The pull request.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One mark per (comment, edited-at), oldest first; empty when none.</returns>
    public ValueTask<Result<IReadOnlyList<PrCommentMark>>> Latest(PrRef pr, CancellationToken cancellationToken);

    /// <summary>Deletes every row of <paramref name="pr"/> (IDEA-033's terminal cleanup), unless another instance still names the PR.</summary>
    /// <param name="pr">The pull request.</param>
    /// <param name="anotherInstanceNamesPr">True when an instance other than the ending one still names the PR: its rows stay.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The number of rows deleted (0 when kept).</returns>
    public ValueTask<Result<int>> Purge(PrRef pr, bool anotherInstanceNamesPr, CancellationToken cancellationToken);
}
