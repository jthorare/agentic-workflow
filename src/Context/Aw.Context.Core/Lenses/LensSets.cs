using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>
/// The union of several anchors' lenses (scoping spec C1, schema 2). Each anchor's own scope and cascade are computed as for one anchor (<see cref="LensScope"/>); the bands are merged:
/// a product node appears once, in the first list that holds it across the anchors (own, neighbors, affects) with its minimum depth, and lists every anchor that reached it.
/// </summary>
internal static class LensSets
{
    private const int OwnList = 0;
    private const int NeighborList = 1;
    private const int AffectList = 2;

    internal static Result<LensesSetView> Build(LensBasis basis, GraphSnapshot graph, IReadOnlyList<string> given, int depth, IReadOnlyList<DecisionConflict> pairs)
    {
        var distinct = given.Distinct(StringComparer.Ordinal).ToList();
        if (QueryRules.CheckAnchors(distinct) is { } tooMany)
        {
            return tooMany;
        }

        var resolved = AnchorSet.Resolve(graph, distinct, depth, basis.Cascade);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var (scopes, missing) = resolved.Value;
        var intent = Intent(scopes);
        var (product, productKeys, ownUnion) = Product(scopes, depth);
        var (code, codeKeys) = Code(graph, scopes, ownUnion);
        var items = productKeys.Concat(codeKeys).Concat(scopes.SelectMany(s => s.IntoScope(EdgeType.Mentions, EdgeType.Decides).Select(e => e.From.Value))).ToHashSet(StringComparer.Ordinal);
        var bands = new LensBands(intent, product, code);
        var keys = scopes.Select(s => s.Anchor.Key.Value).ToList();
        var coverage = AnchorSet.Coverage(basis, graph, scopes, ownUnion);
        return new LensesSetView(2, [.. scopes.Select(Anchor)], missing, basis.Map, basis.Draft, depth, basis.AsOf, bands, LensEdges.Across(graph, items),
            Conflicts(scopes, pairs), coverage, LensSearchedBuilder.For(basis, keys, depth, bands), [], new Dictionary<string, Citation>(StringComparer.Ordinal))
        {
            MapCommit = basis.MapCommit,
            SkippedComponents = Skipped(scopes),
        };
    }

    // Each skipped unit once, across the anchors, ordinal by unit; null when none.
    private static List<SkippedComponent>? Skipped(List<LensScope> scopes)
    {
        var all = scopes.SelectMany(s => s.Cascade.SkippedComponents).GroupBy(c => c.Unit, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.OrderByDescending(c => c.Features).First()).ToList();
        return all.Count > 0 ? all : null;
    }

    private static LensAnchor Anchor(LensScope scope) =>
        new(scope.Anchor.Key, scope.Anchor.Label, scope.Anchor.Key.Kind, TrustPolicy.ForNode(scope.Anchor, LensScope.Confirmed(scope.Anchor.Provenance)));

    private static List<LensConflict> Conflicts(List<LensScope> scopes, IReadOnlyList<DecisionConflict> pairs) =>
        [.. scopes.SelectMany(s => LensConflicts.Of(s, pairs)).DistinctBy(c => (c.Kind, c.Node.Value, c.Decision.Value, c.Other))];

    private static IntentBand Intent(List<LensScope> scopes)
    {
        var (recordList, decisionList) = IntentUnion(scopes);
        return new IntentBand([.. recordList], [.. decisionList], new IntentCounts(recordList.Count, decisionList.Count, Orphaned: null), Truncated: false);
    }

    /// <summary>The records and decisions of every scope, each once with the anchors that reached it, in key order and uncapped (the history lens cuts them by time).</summary>
    internal static (List<LensRecord> Records, List<LensDecision> Decisions) IntentUnion(List<LensScope> scopes)
    {
        var records = new Dictionary<string, (LensRecord Record, List<NodeKey> Mentions, SortedSet<int> By)>(StringComparer.Ordinal);
        var decisions = new Dictionary<string, (LensDecision Decision, List<NodeKey> Decides, List<NodeKey> Supersedes, SortedSet<int> By)>(StringComparer.Ordinal);
        for (var i = 0; i < scopes.Count; i++)
        {
            foreach (var r in LensIntent.Records(scopes[i]))
            {
                var entry = records.GetValueOrDefault(r.Key.Value, (r, [], []));
                entry.Mentions.AddRange(r.Mentions);
                _ = entry.By.Add(i);
                records[r.Key.Value] = entry;
            }

            foreach (var d in LensIntent.Decisions(scopes[i]))
            {
                var entry = decisions.GetValueOrDefault(d.Node.Value, (d, [], [], []));
                entry.Decides.AddRange(d.Decides);
                entry.Supersedes.AddRange(d.Supersedes);
                _ = entry.By.Add(i);
                decisions[d.Node.Value] = entry;
            }
        }

        var recordList = records.Values.OrderBy(e => e.Record.Key.Value, StringComparer.Ordinal).Select(e => e.Record with
        {
            Mentions = Sorted(e.Mentions),
            Anchors = Names(scopes, e.By),
            Depth = 0,
        }).ToList();
        var decisionList = decisions.Values.OrderBy(e => e.Decision.Node.Value, StringComparer.Ordinal).Select(e => e.Decision with
        {
            Decides = Sorted(e.Decides),
            Supersedes = Sorted(e.Supersedes),
            Anchors = Names(scopes, e.By),
            Depth = 0,
        }).ToList();
        return (recordList, decisionList);
    }

    private static List<NodeKey> Sorted(IEnumerable<NodeKey> keys) =>
        [.. keys.DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)];

    private static List<string> Names(List<LensScope> scopes, SortedSet<int> by) => [.. by.Select(i => scopes[i].Anchor.Key.Value)];

    private sealed class Reached(LensItem item, int list, int depth, IReadOnlyList<string> path)
    {
        internal LensItem Item { get; set; } = item;

        internal int List { get; set; } = list;

        internal int Depth { get; set; } = depth;

        internal IReadOnlyList<string> Path { get; set; } = path;

        internal SortedSet<int> By { get; } = [];
    }

    private static (ProductBand Band, HashSet<string> Keys, HashSet<string> Own) Product(List<LensScope> scopes, int depth)
    {
        var reached = new Dictionary<string, Reached>(StringComparer.Ordinal);
        var all = new HashSet<string>(StringComparer.Ordinal);
        var own = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < scopes.Count; i++)
        {
            var lists = LensProduct.Lists(scopes[i], depth);
            all.UnionWith(lists.Seen);
            own.UnionWith(scopes[i].Own);
            foreach (var item in lists.Own)
            {
                Note(reached, i, (item, OwnList, 0, []));
            }

            foreach (var r in lists.Neighbors)
            {
                Note(reached, i, (Plain(r), NeighborList, r.Depth, r.Path));
            }

            foreach (var r in lists.Affects)
            {
                Note(reached, i, (Plain(r), AffectList, r.Depth, r.Path));
            }
        }

        List<Reached> In(int list) =>
            [.. reached.Values.Where(r => r.List == list).OrderBy(r => r.Depth).ThenBy(r => r.By.Min).ThenBy(r => r.Item.Node.Value, StringComparer.Ordinal)];
        var ownItems = In(OwnList).Select(r => r.Item with { Anchors = Names(scopes, r.By), Depth = r.Depth }).ToList();
        var neighbors = In(NeighborList).Select(r => Reach(scopes, r)).ToList();
        var affects = In(AffectList).Select(r => Reach(scopes, r)).ToList();
        var band = LensProduct.Band(ownItems, neighbors, affects, LensProduct.Edges(scopes[0].Graph, all));
        return (band, all, own);
    }

    private static LensItem Plain(LensReach r) => new(r.Node, r.Label, r.Kind, r.Provenance, r.Confidence, r.Confirmed, r.Trust);

    private static void Note(Dictionary<string, Reached> reached, int anchor, (LensItem Item, int List, int Depth, IReadOnlyList<string> Path) hit)
    {
        var (item, list, depth, path) = hit;
        if (!reached.TryGetValue(item.Node.Value, out var entry))
        {
            reached[item.Node.Value] = entry = new Reached(item, list, depth, path);
        }
        else if (list < entry.List || (list == entry.List && depth < entry.Depth))
        {
            (entry.Item, entry.List, entry.Depth, entry.Path) = (item, list, depth, path);
        }

        _ = entry.By.Add(anchor);
    }

    private static LensReach Reach(List<LensScope> scopes, Reached r) =>
        new(r.Item.Node, r.Item.Label, r.Item.Kind, r.Item.Provenance, r.Item.Confidence, r.Item.Confirmed, r.Item.Trust, r.Depth, r.Path) { Anchors = Names(scopes, r.By) };

    private static (CodeBand Band, HashSet<string> Keys) Code(GraphSnapshot graph, List<LensScope> scopes, HashSet<string> ownUnion)
    {
        var edges = scopes.SelectMany(s => s.IntoScope(EdgeType.Implements, EdgeType.Tests).Where(e => s.Nodes.ContainsKey(e.From.Value))).DistinctBy(e => (e.From.Value, e.To.Value, e.Type)).ToList();
        var files = LensCode.Files(edges).Select(f => f with { Anchors = ReachedFile(scopes, f), Depth = 0 }).ToList();
        var noCode = LensCode.NoCode(graph, ownUnion.Select(NodeKey.From));
        var keys = scopes.SelectMany(s => s.IntoScope(EdgeType.Implements, EdgeType.Tests).Select(e => e.From.Value)).ToHashSet(StringComparer.Ordinal);
        return (LensCode.Band(files, noCode), keys);
    }

    private static List<string> ReachedFile(List<LensScope> scopes, LensFile file) =>
        [.. scopes.Where(s => s.IntoScope(EdgeType.Implements, EdgeType.Tests).Any(e => e.From.Name == file.Path)).Select(s => s.Anchor.Key.Value)];
}
