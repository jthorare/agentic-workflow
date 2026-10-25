namespace Aw.Context.Core.Search;

/// <summary>What an index answers: how many documents matched and the best of them.</summary>
/// <param name="Total">How many documents matched.</param>
/// <param name="Matches">The best matches first, up to the limit asked.</param>
public sealed record MapSearchPage(int Total, IReadOnlyList<MapSearchMatch> Matches);
