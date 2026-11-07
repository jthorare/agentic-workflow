using Aw.Sindri.Features.SendReviewFix;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/reviews/{owner}/{repo}/{number}/fix (PR review page spec §8.2); the PR comes from the route.</summary>
/// <param name="Kind">check, requirement or line.</param>
/// <param name="Text">What needs fixing (1 to 2,000 characters).</param>
/// <param name="Head">The head sha the finding is on (40 hex digits).</param>
/// <param name="SourceId">The check or requirement id; null for a line.</param>
/// <param name="Path">The file of a diff line; null otherwise.</param>
/// <param name="Line">The line of a diff line; null otherwise.</param>
/// <param name="Side">new or old; null means new.</param>
/// <param name="Item">The item a Retry reuses; null on a first send.</param>
internal sealed record ReviewFixBody(string Kind, string Text, string Head, string? SourceId = null, string? Path = null, int? Line = null, string? Side = null, string? Item = null)
{
    /// <summary>The request for the route's PR.</summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="number">The PR number.</param>
    /// <returns>The request.</returns>
    internal SendReviewFixRequest ToRequest(string owner, string repo, int number) => new(owner, repo, number, Kind, Text, Head, SourceId, Path, Line, Side, Item);
}
