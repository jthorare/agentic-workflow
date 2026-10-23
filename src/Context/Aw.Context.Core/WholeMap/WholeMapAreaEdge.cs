using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>The edges of one type from the nodes of one area to the nodes of another, counted.</summary>
/// <param name="From">The source area's key.</param>
/// <param name="To">The target area's key.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Count">How many edges.</param>
/// <param name="Confirmed">How many of them are confirmed.</param>
public sealed record WholeMapAreaEdge(string From, string To, EdgeType Type, int Count, int Confirmed);
