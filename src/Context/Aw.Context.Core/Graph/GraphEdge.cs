using System.Text.Json.Serialization;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Graph;

/// <summary>A product-graph edge (spec §6.1).</summary>
/// <param name="From">The source node.</param>
/// <param name="To">The target node.</param>
/// <param name="Type">The edge type; <see cref="EdgeRules"/> checks the kinds it joins.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Evidence">The records it rests on (an 'implements' edge's file evidence decides a session's repo scope), or null.</param>
/// <param name="Order">The position in its flow, for 'step-of'; otherwise null.</param>
/// <param name="At">When it was written; an import keeps the file's value.</param>
/// <param name="Count">Only on a roll-up edge (a feature's 'depends-on', 'reads' or 'writes' summed from its files): the distinct file pairs (depends-on) or files behind it; otherwise null, and then omitted from JSON.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphEdge(
    NodeKey From,
    NodeKey To,
    EdgeType Type,
    Provenance Provenance = Provenance.Human,
    double Confidence = 1.0,
    IReadOnlyList<EvidenceRef>? Evidence = null,
    int? Order = null,
    DateTimeOffset? At = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Count = null)
{
    /// <summary>The evidence, never null.</summary>
    [JsonIgnore]
    public IReadOnlyList<EvidenceRef> EvidenceOrEmpty => Evidence ?? [];

    /// <summary>Whether two edges are the same fact (same ends and type).</summary>
    /// <param name="other">The other edge.</param>
    /// <returns>True for the same from, to and type.</returns>
    public bool SameFact(GraphEdge other) =>
        other is not null && From.Value == other.From.Value && To.Value == other.To.Value && Type == other.Type;
}
