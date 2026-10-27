namespace Aw.Context.Core.ScopeGraph;

/// <summary>An edge of the scoping graph, both ends among its nodes.</summary>
/// <param name="From">The source node id.</param>
/// <param name="To">The target node id.</param>
/// <param name="Type">The edge type.</param>
public sealed record ScopeGraphEdge(ScopeGraphNodeId From, ScopeGraphNodeId To, string Type);
