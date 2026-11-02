namespace Aw.Context.Core.ScopeGraph;

/// <summary>
/// The scoping graph (issue 1005): the nodes within the lens depth and hops, ranked, with the edges between them, and what was cut. It is the shape of the scoping 'fact:graph'
/// before its size is counted. A node id is the map's own key, the id context_node and context_neighbors take.
/// </summary>
/// <param name="Version">1.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Anchors">The anchors that resolved.</param>
/// <param name="Depth">How far the answer widened.</param>
/// <param name="Hops">The cascade hops that were walked.</param>
/// <param name="Nodes">The nodes, best rank first.</param>
/// <param name="Edges">Edges with both ends in <paramref name="Nodes"/>.</param>
/// <param name="Limits">What was left out and why.</param>
public sealed record ScopeGraphView(
    int Version,
    string MapVersion,
    IReadOnlyList<string> Anchors,
    int Depth,
    IReadOnlyList<string> Hops,
    IReadOnlyList<ScopeGraphNode> Nodes,
    IReadOnlyList<ScopeGraphEdge> Edges,
    IReadOnlyList<ScopeGraphLimit> Limits);
