namespace Aw.Context.Core.Search;

/// <summary>The answer to a map search: the hits best first, and how many nodes matched in all.</summary>
/// <param name="MapVersion">The map read ('v13'; the draft's own number for a draft).</param>
/// <param name="Total">How many nodes matched; <paramref name="Hits"/> holds the best of them up to the limit.</param>
/// <param name="Hits">The hits, best first.</param>
public sealed record MapSearchView(string MapVersion, int Total, IReadOnlyList<MapSearchHit> Hits);
