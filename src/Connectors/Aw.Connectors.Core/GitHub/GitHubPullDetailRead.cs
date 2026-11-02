namespace Aw.Connectors.Core.GitHub;

/// <summary>What reading one pull request's detail returned.</summary>
/// <param name="Detail">The detail, or null when the rate limit stopped the read.</param>
/// <param name="RateLimitedUntil">Set when GitHub's rate limit stopped the read; the next run should wait until this time.</param>
public sealed record GitHubPullDetailRead(GitHubPullDetail? Detail, DateTimeOffset? RateLimitedUntil);
