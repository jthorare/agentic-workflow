namespace Aw.Context.Core.ScopeGraph;

/// <summary>One node of the scoping graph.</summary>
/// <param name="Id">The map key (or the record id, or <c lang="text">code:path#symbol</c> for a symbol, or <c lang="text">concept:&lt;slug&gt;</c> for a concept): stable across calls on one map version.</param>
/// <param name="Kind">The node kind: feature, surface, action, thing, code, record, concept and so on.</param>
/// <param name="Path">The repo path, present only on a <c lang="text">code:</c> node (derived from its key); null for every other node.</param>
/// <param name="Symbol">The symbol, set only on a symbol row from the code index; else null.</param>
/// <param name="Summary">The node's label for every kind except concept, where it is the concept's summary attribute; never synthesized.</param>
/// <param name="Rank">1 for the most relevant.</param>
/// <param name="Lens">The lens it was found by: product, related, code or history.</param>
/// <param name="LensItem">The lens item id ('L-code-3') the gather numbered it.</param>
/// <param name="Band">The band: own, neighbors, affects, code or intent (a record or decision node), or null when the node was reached only as an edge end.</param>
/// <param name="Citations">Where it is in the record store.</param>
public sealed record ScopeGraphNode(ScopeGraphNodeId Id, string Kind, string? Path, string? Symbol, string Summary, int Rank, string Lens, string LensItem, string? Band, IReadOnlyList<ScopeGraphCitation> Citations);
