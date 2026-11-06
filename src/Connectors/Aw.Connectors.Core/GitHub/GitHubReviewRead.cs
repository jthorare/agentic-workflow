namespace Aw.Connectors.Core.GitHub;

/// <summary>What reading one pull request's reviews returned.</summary>
/// <param name="Reviews">Each reviewer's latest review, with the new-commit count; empty when the rate limit stopped the read.</param>
/// <param name="RateLimitedUntil">Set when GitHub's rate limit stopped the read; the next run should wait until this time.</param>
public sealed record GitHubReviewRead(IReadOnlyList<ConnectorReview> Reviews, DateTimeOffset? RateLimitedUntil);
