namespace Aw.Context.Core.ScopeGraph;

/// <summary>A node the lenses found, before it is ranked: its key, the summary, where it was found and how far from the anchors.</summary>
/// <param name="Id">The map key.</param>
/// <param name="Summary">The cleaned label.</param>
/// <param name="Lens">The lens it was found by.</param>
/// <param name="Band">The band.</param>
/// <param name="Group">The product-first order: 0 anchors, 1 own, 2 neighbors, 3 affects, 4 intent, 5 code.</param>
/// <param name="Distance">Hops from the anchors, for ordering inside a group.</param>
internal sealed record ScopeGraphCandidate(string Id, string Summary, string Lens, string Band, int Group, int Distance);
