namespace Aw.Context.Core.Lenses;

/// <summary>The product band: the own scope, then neighbors, then what the anchor affects; a node appears once, in the first list that holds it.</summary>
/// <param name="Own">The anchor, its descendant features, their surfaces and those surfaces' actions.</param>
/// <param name="Neighbors">What 'neighbors' returns at the depth, minus the own scope.</param>
/// <param name="Affects">What the cascade reaches, minus the lists above.</param>
/// <param name="Counts">The full counts.</param>
/// <param name="Edges">The edges whose two ends are product items.</param>
/// <param name="Truncated">Always false: a band lists every item (kept so the wire shape stays).</param>
public sealed record ProductBand(
    IReadOnlyList<LensItem> Own,
    IReadOnlyList<LensReach> Neighbors,
    IReadOnlyList<LensReach> Affects,
    ProductCounts Counts,
    IReadOnlyList<LensEdge> Edges,
    bool Truncated);
