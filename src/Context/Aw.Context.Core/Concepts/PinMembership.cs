using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Reads what <see cref="PinMoves"/> compares out of the concepts reads of two map versions.</summary>
public static class PinMembership
{
    private const string Prefix = "concept:";

    /// <summary>The moves of every visible pin of the later version between two reads.</summary>
    /// <param name="fromVersion">The earlier version's number.</param>
    /// <param name="toVersion">The later version's number.</param>
    /// <param name="before">The concepts read of the earlier version.</param>
    /// <param name="after">The concepts read of the later version.</param>
    /// <returns>One move per pin the later version shows; empty when the caller sees no pin there.</returns>
    public static IReadOnlyList<PinMove> Moves(int fromVersion, int toVersion, ConceptRead before, ConceptRead after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (after.Pins is not { IsEmpty: false } shown)
        {
            return [];
        }

        var wasMembers = before.Pins is { } was ? MembersOf(before.Graph, was.Visible) : new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        return PinMoves.Between(fromVersion, toVersion, wasMembers, MembersOf(after.Graph, shown.Visible), PlacesIn(after.Graph));
    }

    private static Dictionary<string, IReadOnlySet<string>> MembersOf(GraphSnapshot graph, IReadOnlyList<ConceptId> pins)
    {
        var members = pins.ToDictionary(p => p.Value, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e is { Type: EdgeType.BelongsTo, From.Kind: NodeKind.Feature } && e.To.Value.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            if (members.TryGetValue(edge.To.Value[Prefix.Length..], out var set))
            {
                set.Add(edge.From.Value);
            }
        }

        return members.ToDictionary(m => m.Key, m => (IReadOnlySet<string>)m.Value, StringComparer.Ordinal);
    }

    private static Dictionary<string, IReadOnlyList<FeaturePlace>> PlacesIn(GraphSnapshot graph)
    {
        var labels = graph.Nodes.Where(n => n.Key.Kind == NodeKind.Concept).ToDictionary(n => n.Key.Value, n => n.Label, StringComparer.Ordinal);
        var places = new Dictionary<string, List<FeaturePlace>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e is { Type: EdgeType.BelongsTo, From.Kind: NodeKind.Feature } && e.To.Value.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            if (labels.TryGetValue(edge.To.Value, out var label) && ConceptId.TryFrom(edge.To.Value[Prefix.Length..], out var id))
            {
                (places.TryGetValue(edge.From.Value, out var list) ? list : places[edge.From.Value] = []).Add(new FeaturePlace(id, label));
            }
        }

        return places.ToDictionary(p => p.Key, p => (IReadOnlyList<FeaturePlace>)p.Value, StringComparer.Ordinal);
    }
}
