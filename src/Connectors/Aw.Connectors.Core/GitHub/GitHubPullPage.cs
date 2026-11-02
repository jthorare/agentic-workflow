namespace Aw.Connectors.Core.GitHub;

/// <summary>What one run over a repo's issue list read.</summary>
/// <param name="Items">The entries not seen before, oldest change first.</param>
/// <param name="Next">Where the next run starts; equals the cursor the run began with when nothing new came.</param>
/// <param name="RateLimitedUntil">Set when GitHub's rate limit stopped the run: <paramref name="Items"/> and <paramref name="Next"/> are what was read before it, and the next run should wait until this time. Null when the run reached its end or its limit.</param>
public sealed record GitHubPullPage(IReadOnlyList<GitHubIssue> Items, GitHubCursor Next, DateTimeOffset? RateLimitedUntil);
