namespace Aw.UiDriver.Core.Protocol;

/// <summary>The elements a search matched, in tree order, without their children.</summary>
/// <param name="Matches">The matches (at most the limit).</param>
/// <param name="Total">How many elements matched in all.</param>
internal sealed record SearchResponse(IReadOnlyList<NodeInfo> Matches, int Total);
