namespace Aw.CodeIndex.Core.Model;

/// <summary>What a graph run returned, already checked against the snapshot's file list and the node and edge caps.</summary>
/// <param name="Nodes">The symbol nodes.</param>
/// <param name="Edges">The call edges, both ends among the nodes.</param>
/// <param name="Dropped">How many records were dropped (malformed, outside the snapshot, or naming no kept node); a count, never what was in them.</param>
public sealed record GraphSnapshot(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges, int Dropped = 0);
