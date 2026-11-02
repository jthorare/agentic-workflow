namespace Aw.Context.Core.Graph;

/// <summary>An edge as 'context show' answers it: the <see cref="EdgeFact"/> fields and the kind and label of the node at the other end.</summary>
/// <param name="From">The source node.</param>
/// <param name="Type">The type.</param>
/// <param name="To">The target node.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confirmed">True when a human confirmed it.</param>
/// <param name="Trust">Whether it can be trusted.</param>
/// <param name="Order">Its position in a flow ('step-of'), or null.</param>
/// <param name="OtherKind">The kind of the node at the other end from the one shown ('to' for an out edge, 'from' for an in edge).</param>
/// <param name="OtherLabel">That node's label, written like <see cref="NodeFact.Label"/> (fenced when untrusted); null when the node is not in the caller's map.</param>
/// <param name="Count">A roll-up edge's count of file pairs (or files), otherwise null.</param>
/// <param name="Example">A roll-up edge's first cited pair or file, otherwise null.</param>
public sealed record EdgeDetail(
    NodeKey From,
    EdgeType Type,
    NodeKey To,
    Results.Provenance Provenance,
    bool Confirmed,
    Results.Trust Trust,
    int? Order,
    NodeKind OtherKind,
    string? OtherLabel,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Count = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Example = null)
{
    /// <summary>The detail for <paramref name="edge"/>.</summary>
    /// <param name="edge">The edge fact.</param>
    /// <param name="otherKind">The other end's kind.</param>
    /// <param name="otherLabel">The other end's label, or null.</param>
    /// <returns>The detail.</returns>
    public static EdgeDetail Of(EdgeFact edge, NodeKind otherKind, string? otherLabel)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return new EdgeDetail(edge.From, edge.Type, edge.To, edge.Provenance, edge.Confirmed, edge.Trust, edge.Order, otherKind, otherLabel, edge.Count, edge.Example);
    }

    /// <summary>The plain fact (what the text form prints).</summary>
    /// <returns>The fact.</returns>
    public EdgeFact ToFact() => new(From, Type, To, Provenance, Confirmed, Trust, Order);
}
