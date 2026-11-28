namespace Aw.Context.Core.Records;

/// <summary>What a pull request record carries beside its text (PR review page spec §5.6): who is asked to review it, its head commit and each reviewer's latest reviewed commit.</summary>
/// <param name="RequestedReviewers">The logins whose review is still requested.</param>
/// <param name="HeadSha">The head commit, or null when the source gave none.</param>
/// <param name="Reviews">Each reviewer's latest review; read for open pull requests only.</param>
/// <param name="Mergeable">Whether the forge said the pull request can merge (false is a conflict); null for a record stored before it was kept, or while the forge had not worked it out, which reads as unknown (#4333).</param>
public sealed record PullFacts(IReadOnlyList<string> RequestedReviewers, string? HeadSha, IReadOnlyList<ReviewedCommit> Reviews, bool? Mergeable = null);
