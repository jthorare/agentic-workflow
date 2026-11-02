using Aw.Context.Core.Results;

namespace Aw.Context.Core.Graph;

/// <summary>An edge as an answer shows it.</summary>
/// <param name="From">The source node.</param>
/// <param name="Type">The type.</param>
/// <param name="To">The target node.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confirmed">True when a human confirmed it.</param>
/// <param name="Trust">Whether it can be trusted.</param>
/// <param name="Order">Its position in a flow ('step-of'), or null.</param>
/// <param name="Count">A roll-up edge's count of file pairs (or files), otherwise null.</param>
/// <param name="Example">A roll-up edge's first cited pair or file as repo-relative paths ('a/b1.ts -> c/o1.ts'), otherwise null.</param>
public sealed record EdgeFact(NodeKey From, EdgeType Type, NodeKey To, Provenance Provenance, bool Confirmed, Results.Trust Trust, int? Order = null, int? Count = null, string? Example = null)
{
    /// <summary>The fact for <paramref name="edge"/>: confirmed when human, trust from <see cref="TrustPolicy"/>.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>The fact.</returns>
    public static EdgeFact Of(GraphEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        var confirmed = edge.Provenance == Provenance.Human;
        return new EdgeFact(edge.From, edge.Type, edge.To, edge.Provenance, confirmed, TrustPolicy.For(edge.Provenance, confirmed), edge.Order, edge.Count, ExampleOf(edge));
    }

    // The first cited pair or file of a roll-up edge as repo-relative paths: a 'depends-on' pair is the importing file (key) and the imported file (span), a 'reads' or 'writes' citation one file.
    private static string? ExampleOf(GraphEdge edge)
    {
        if (edge.Count is null || edge.EvidenceOrEmpty.Count == 0)
        {
            return null;
        }

        static string Path(string record) => record.StartsWith("repo:", StringComparison.Ordinal) && record.IndexOf('/', StringComparison.Ordinal) is var slash and > 0 ? record[(slash + 1)..] : record;
        var first = edge.EvidenceOrEmpty[0];
        return first.Span is { } target && target.StartsWith("repo:", StringComparison.Ordinal) ? Path(first.Key.Value) + " -> " + Path(target) : Path(first.Key.Value);
    }
}
