namespace Aw.Connectors.Core.GitHub;

/// <summary>What one comment read returned.</summary>
/// <param name="Items">The comments read, oldest change first (repo-wide read) or oldest first (one issue).</param>
/// <param name="RateLimitedUntil">Set when GitHub's rate limit stopped the read: <paramref name="Items"/> is what was read before it and is incomplete. Null when the read reached its end.</param>
public sealed record GitHubCommentPage(IReadOnlyList<GitHubComment> Items, DateTimeOffset? RateLimitedUntil);
