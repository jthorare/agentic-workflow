using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>The edges the owner declared on the pins, and how many entries had no endpoint in the map.</summary>
/// <param name="Edges">The Human edges.</param>
/// <param name="Skipped">The entries skipped.</param>
public sealed record DeclaredEdges(IReadOnlyList<GraphEdge> Edges, int Skipped);
