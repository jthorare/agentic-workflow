using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.WholeMap;

/// <summary>One edge of the whole map.</summary>
/// <param name="From">The source node.</param>
/// <param name="To">The target node.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Link">The edge's id.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Confirmed">False for a model-suggested edge nobody accepted.</param>
public sealed record WholeMapEdge(NodeKey From, NodeKey To, EdgeType Type, LinkId Link, Provenance Provenance, double Confidence, bool Confirmed);
