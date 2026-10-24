using Aw.Context.Core.Map;

namespace Aw.Context.Core.Subgraph;

/// <summary>A subgraph question (scoping spec C14): node keys, how far to widen, and which map to read.</summary>
/// <param name="Keys">The node keys, as given (1 or more; a QUERY body has no upper bound).</param>
/// <param name="Depth">How many edges to widen through, 0 to 4; null reads as 0.</param>
/// <param name="Read">Which map to read.</param>
public sealed record SubgraphQuery(IReadOnlyList<string> Keys, int? Depth, MapRead Read);
