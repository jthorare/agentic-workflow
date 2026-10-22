namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// One row of 'pr_comment_marks' (PR babysitter spec §5.4, W26): what a babysitter did with a comment. The latest row per
/// (comment, edited-at) by <see cref="At"/> is that comment's state: an edited comment is unhandled again.
/// </summary>
/// <param name="Pr">The pull request.</param>
/// <param name="CommentId">The comment (or its thread's) id on the forge.</param>
/// <param name="EditedAt">The comment's edit stamp the mark answers, or null for one never edited.</param>
/// <param name="Instance">The babysitter instance that wrote the mark.</param>
/// <param name="Outcome">The outcome ('thread', 'reaction', 'fixed', 'fixedReplies', 'none', 'held').</param>
/// <param name="Reason">Why (a held comment's type or failed branch), or null.</param>
/// <param name="Commit">The commit that fixed it, or null.</param>
/// <param name="At">When the mark was written.</param>
/// <param name="Effect">The identity of the effect that delivered it; a mark with an identity already recorded is not written twice.</param>
public sealed record PrCommentMark(
    PrRef Pr, PrCommentId CommentId, string? EditedAt, InstanceAddress Instance, string Outcome, string? Reason, string? Commit, DateTimeOffset At, string Effect);
