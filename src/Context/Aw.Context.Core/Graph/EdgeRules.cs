namespace Aw.Context.Core.Graph;

/// <summary>Which node kinds each edge type joins (spec §6.1's edge table), and each type's name in map files.</summary>
public static class EdgeRules
{
    private static readonly NodeKind[] _any = Enum.GetValues<NodeKind>();
    private static readonly NodeKind[] _implemented = [NodeKind.Feature, NodeKind.Surface, NodeKind.Action];
    private static readonly NodeKind[] _members = [NodeKind.Feature, NodeKind.Record, NodeKind.Decision];
    private static readonly NodeKind[] _onSurface = [NodeKind.Surface, NodeKind.Action];

    private static readonly Dictionary<EdgeType, (string Name, NodeKind[] From, NodeKind[] To)> _table = new()
    {
        [EdgeType.PartOf] = ("part-of", [NodeKind.Feature, NodeKind.Surface], [NodeKind.Feature]),
        [EdgeType.On] = ("on", [NodeKind.Action], [NodeKind.Surface]),
        [EdgeType.MovesTo] = ("moves-to", [NodeKind.Surface], [NodeKind.Surface]),
        [EdgeType.Reads] = ("reads", _onSurface, [NodeKind.Thing]),
        [EdgeType.Writes] = ("writes", _onSurface, [NodeKind.Thing]),
        [EdgeType.DependsOn] = ("depends-on", [NodeKind.Feature], [NodeKind.Feature]),
        [EdgeType.StepOf] = ("step-of", _onSurface, [NodeKind.Flow]),
        [EdgeType.Implements] = ("implements", [NodeKind.Code], _implemented),
        [EdgeType.Tests] = ("tests", [NodeKind.Code], _implemented),
        [EdgeType.Decides] = ("decides", [NodeKind.Decision], _any),
        [EdgeType.Mentions] = ("mentions", [NodeKind.Record], _any),
        [EdgeType.Quotes] = ("quotes", [NodeKind.Decision], [NodeKind.Record]),
        [EdgeType.BelongsTo] = ("belongs-to", _members, [NodeKind.Concept]),
        [EdgeType.Supersedes] = ("supersedes", [NodeKind.Decision], [NodeKind.Decision]),
    };

    /// <summary>Whether an edge of <paramref name="type"/> may go from a <paramref name="from"/> node to a <paramref name="to"/> node.</summary>
    /// <param name="type">The edge type.</param>
    /// <param name="from">The source node's kind.</param>
    /// <param name="to">The target node's kind.</param>
    /// <returns>True when the edge table allows it.</returns>
    public static bool Allows(EdgeType type, NodeKind from, NodeKind to) =>
        _table.TryGetValue(type, out var rule) && rule.From.Contains(from) && rule.To.Contains(to);

    /// <summary>The edge type as map files write it.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Its name, such as 'part-of'.</returns>
    public static string Name(EdgeType type) => _table[type].Name;
}
