namespace Aw.Connectors.Core;

/// <summary>What a pull request record carries beside its text: who is asked to review it, its head commit and each reviewer's latest reviewed commit.</summary>
/// <param name="RequestedReviewers">The logins whose review is still requested.</param>
/// <param name="HeadSha">The head commit, or null when unknown.</param>
/// <param name="Reviews">Each reviewer's latest review (open pull requests only; empty otherwise).</param>
/// <param name="Mergeable">Whether GitHub says the pull request can merge ('mergeable'; false is a conflict), or null when it said nothing yet.</param>
/// <param name="HeadRef">The head branch name, or null when unknown.</param>
public sealed record ConnectorPullFacts(IReadOnlyList<string> RequestedReviewers, string? HeadSha, IReadOnlyList<ConnectorReview> Reviews, bool? Mergeable = null, string? HeadRef = null);
