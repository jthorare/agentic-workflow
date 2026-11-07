namespace Aw.Sindri.Features.LinearIssues;

/// <summary>One stored issue read by its key, with the names resolved as in the list and no link or PR yet.</summary>
/// <param name="Row">The issue.</param>
/// <param name="Description">The description; empty when the issue has none.</param>
/// <param name="Removed">True when Linear removed the issue: this is its last stored copy.</param>
/// <param name="Comments">The newest stored comments, newest first.</param>
internal sealed record LinearIssueDetailFacts(LinearIssueRowView Row, string Description, bool Removed, IReadOnlyList<LinearCommentView> Comments);
