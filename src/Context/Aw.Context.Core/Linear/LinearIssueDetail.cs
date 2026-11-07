namespace Aw.Context.Core.Linear;

/// <summary>One stored issue read by its human key, with what a page needs beside it (Linear hub spec 2026-11-04 §6.1).</summary>
/// <param name="Issue">The issue as the record-key read gives it, description filled.</param>
/// <param name="Removed">True when the issue was removed in Linear: <paramref name="Issue"/> is then its last stored copy.</param>
/// <param name="Parent">The parent's key and title, or null when there is no parent or it isn't stored or visible to the caller.</param>
/// <param name="Comments">The issue's newest stored comments (at most <see cref="LinearIssueSide.MaxComments"/>), newest first.</param>
public sealed record LinearIssueDetail(LinearIssueRow Issue, bool Removed, LinearIssueParent? Parent, IReadOnlyList<LinearCommentRow> Comments);
