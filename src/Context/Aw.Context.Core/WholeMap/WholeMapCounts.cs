namespace Aw.Context.Core.WholeMap;

/// <summary>What the map holds, in full whatever the answer's detail.</summary>
/// <param name="Nodes">Every node the caller may see.</param>
/// <param name="Edges">Every edge between them.</param>
/// <param name="ByKind">Nodes per kind, every kind present (zero included), in kind order.</param>
public sealed record WholeMapCounts(int Nodes, int Edges, IReadOnlyDictionary<string, int> ByKind);
