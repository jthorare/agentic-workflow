using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Features.PullGitHub;

/// <summary>What a GitHub pull read.</summary>
/// <param name="Items">The records, oldest change first.</param>
/// <param name="Next">Where the next pull starts.</param>
/// <param name="RateLimitedUntil">Set when GitHub's rate limit stopped the run; the next pull should wait until then.</param>
public sealed record PullGitHubResponse(IReadOnlyList<ConnectorItem> Items, GitHubCursor Next, DateTimeOffset? RateLimitedUntil);
