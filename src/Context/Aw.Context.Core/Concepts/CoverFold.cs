using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>A graph with the covered derived concepts dropped, and how many there were.</summary>
/// <param name="Graph">The graph without them.</param>
/// <param name="Covered">How many derived concepts were dropped.</param>
public sealed record CoverFold(GraphSnapshot Graph, int Covered);
