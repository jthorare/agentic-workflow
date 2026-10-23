using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>
/// The rules of <see cref="MapAreas"/> over one graph. The area-features are the roots of the part-of chains (the smallest parent at each step, the smallest key on a cycle), made deeper when
/// one would hold more than half the nodes or more than the area cap, or when there are fewer than 3 (at most two passes, each replacing a feature by its child features; the replaced feature
/// and what reaches only it are unassigned). A feature's area is the area-feature at or above it; every other node
/// takes the area most of the nodes it points at (or that point at it) are in, one vote per edge, ties to the smallest key, the other areas it reaches in 'also in' (at most five).
/// An area no vote reaches is the unassigned group, and unassigned votes count for nothing.
/// </summary>
internal sealed class AreaAssigner
{
    private const int AlsoInCap = 5;
    private readonly Dictionary<string, NodeKind> _kinds;
    private readonly Dictionary<string, List<GraphEdge>> _out = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<GraphEdge>> _in = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NodeArea> _areas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _parents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _chains = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _roots = new(StringComparer.Ordinal);
    private readonly int _areaCap;

    internal AreaAssigner(GraphSnapshot graph, int areaCap)
    {
        _areaCap = areaCap;
        _kinds = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Key.Kind, StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => _kinds.ContainsKey(e.From.Value) && _kinds.ContainsKey(e.To.Value)))
        {
            Add(_out, edge.From.Value, edge);
            Add(_in, edge.To.Value, edge);
        }

        foreach (var feature in Keys(NodeKind.Feature))
        {
            _parents[feature] = [.. Targets(feature, EdgeType.PartOf).Where(t => _kinds[t] == NodeKind.Feature).Order(StringComparer.Ordinal)];
        }

        foreach (var feature in _parents.Keys)
        {
            (_chains[feature], _roots[feature]) = Chain(feature);
        }
    }

    internal AreaIndex Run()
    {
        var set = _roots.Values.ToHashSet(StringComparer.Ordinal);
        for (var pass = 0; pass < 2; pass++)
        {
            Assemble(set);
            if (Deeper(set) is not { } next)
            {
                break;
            }

            set = next;
        }

        Assemble(set);
        foreach (var key in _kinds.Keys.Where(k => !_areas.ContainsKey(k)))
        {
            _areas[key] = new NodeArea(AreaIndex.UnassignedKey, []);
        }

        return new AreaIndex(_areas);
    }

    private void Assemble(HashSet<string> areaFeatures)
    {
        _areas.Clear();
        AssignFeatures(areaFeatures);
        Vote(NodeKind.Surface, key => Targets(key, EdgeType.PartOf));
        Vote(NodeKind.Action, key => Targets(key, EdgeType.On));
        Vote(NodeKind.Flow, key => Sources(key, EdgeType.StepOf));
        Vote(NodeKind.Thing, key => Sources(key, EdgeType.Reads, EdgeType.Writes));
        Vote(NodeKind.Code, key => Targets(key, EdgeType.Implements, EdgeType.Tests));
        AssignIntent();
    }

    // The next set of area-features: each one holding over half the nodes or over the cap is replaced by its child features, and so is the largest when there are fewer than 3; null when nothing can be.
    private HashSet<string>? Deeper(HashSet<string> set)
    {
        var sizes = set.ToDictionary(a => a, a => _areas.Values.Count(v => v.Area == a), StringComparer.Ordinal);
        var children = set.ToDictionary(a => a, a => _parents.Where(p => p.Value.Count > 0 && p.Value[0] == a && p.Key != a).Select(p => p.Key).ToList(), StringComparer.Ordinal);
        var replaced = set.Where(a => children[a].Count > 0 && (sizes[a] * 2 > _kinds.Count || sizes[a] > _areaCap)).ToHashSet(StringComparer.Ordinal);
        if (replaced.Count == 0 && set.Count < 3)
        {
            var largest = set.Where(a => children[a].Count > 0).OrderByDescending(a => sizes[a]).ThenBy(a => a, StringComparer.Ordinal).FirstOrDefault();
            if (largest is not null)
            {
                _ = replaced.Add(largest);
            }
        }

        return replaced.Count == 0 ? null : [.. set.Except(replaced, StringComparer.Ordinal), .. replaced.SelectMany(a => children[a])];
    }

    private static void Add(Dictionary<string, List<GraphEdge>> index, string key, GraphEdge edge)
    {
        if (!index.TryGetValue(key, out var list))
        {
            index[key] = list = [];
        }

        list.Add(edge);
    }

    private IEnumerable<string> Targets(string key, params EdgeType[] types) =>
        _out.TryGetValue(key, out var edges) ? edges.Where(e => types.Contains(e.Type)).Select(e => e.To.Value) : [];

    private IEnumerable<string> Sources(string key, params EdgeType[] types) =>
        _in.TryGetValue(key, out var edges) ? edges.Where(e => types.Contains(e.Type)).Select(e => e.From.Value) : [];

    private IEnumerable<string> Keys(NodeKind kind) => _kinds.Where(p => p.Value == kind).Select(p => p.Key).Order(StringComparer.Ordinal);

    // The chain of a feature up through the smallest parents (itself first) and its root: the last one, or the smallest key on the cycle the chain meets.
    private (List<string> Chain, string Root) Chain(string start)
    {
        var path = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = start;
        while (seen.Add(current))
        {
            path.Add(current);
            if (_parents[current].Count == 0)
            {
                return (path, current);
            }

            current = _parents[current][0];
        }

        return (path, path.Skip(path.IndexOf(current)).Order(StringComparer.Ordinal).First());
    }

    private string? Reach(string feature, HashSet<string> areaFeatures) => _chains[feature].Find(areaFeatures.Contains);

    private void AssignFeatures(HashSet<string> areaFeatures)
    {
        foreach (var (feature, above) in _parents)
        {
            if (Reach(feature, areaFeatures) is not { } area)
            {
                continue;
            }

            var others = above.Skip(1).Select(p => Reach(p, areaFeatures)).OfType<string>().Where(r => r != area).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(AlsoInCap);
            _areas[feature] = new NodeArea(area, [.. others]);
        }
    }

    private void Vote(NodeKind kind, Func<string, IEnumerable<string>> reaches)
    {
        foreach (var key in Keys(kind))
        {
            if (Tally(reaches(key)) is { } area)
            {
                _areas[key] = area;
            }
        }
    }

    // Decisions and records lean on each other and on code, so they settle in passes: each pass reads only what earlier passes settled.
    private void AssignIntent()
    {
        var pending = Keys(NodeKind.Decision).Concat(Keys(NodeKind.Record)).ToList();
        while (pending.Count > 0)
        {
            var settled = new List<(string Key, NodeArea Area)>();
            foreach (var key in pending)
            {
                if (Tally(IntentReaches(key)) is { } area)
                {
                    settled.Add((key, area));
                }
            }

            if (settled.Count == 0)
            {
                return;
            }

            foreach (var (key, area) in settled)
            {
                _areas[key] = area;
            }

            pending = [.. pending.Where(k => !_areas.ContainsKey(k))];
        }
    }

    private IEnumerable<string> IntentReaches(string key)
    {
        if (_kinds[key] == NodeKind.Record)
        {
            return Targets(key, EdgeType.Mentions);
        }

        var decided = Targets(key, EdgeType.Decides).ToList();
        return decided.Exists(HasArea) ? decided : Targets(key, EdgeType.Quotes);
    }

    private bool HasArea(string key) => _areas.TryGetValue(key, out var area) && area.Area != AreaIndex.UnassignedKey;

    private NodeArea? Tally(IEnumerable<string> reached)
    {
        var votes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in reached.Where(HasArea))
        {
            var area = _areas[key].Area;
            votes[area] = votes.GetValueOrDefault(area) + 1;
        }

        if (votes.Count == 0)
        {
            return null;
        }

        var ranked = votes.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key).ToList();
        return new NodeArea(ranked[0], [.. ranked.Skip(1).Take(AlsoInCap)]);
    }
}
