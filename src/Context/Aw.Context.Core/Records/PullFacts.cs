namespace Aw.Context.Core.Records;

/// <summary>What a pull request record carries beside its text (PR review page spec §5.6): who is asked to review it, its head commit and each reviewer's latest reviewed commit.</summary>
/// <param name="RequestedReviewers">The logins whose review is still requested.</param>
/// <param name="HeadSha">The head commit, or null when the source gave none.</param>
/// <param name="Reviews">Each reviewer's latest review; read for open pull requests only.</param>
public sealed record PullFacts(IReadOnlyList<string> RequestedReviewers, string? HeadSha, IReadOnlyList<ReviewedCommit> Reviews);
