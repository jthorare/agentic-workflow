using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Map;

/// <summary>
/// What the repos' deny globs take out of a map (context spec §3, "Denied paths"), decided once, from the stored nodes and from the nodes the overrides add
/// (a pending proposal), never from a graph an override has already changed: a node a person rejected, deleted or merged away is still judged. It takes the code nodes
/// of denied paths, the record nodes of denied repo documents and the decisions quoting them, the nodes whose only evidence is a denied file (model-made or
/// deterministic in the stored graph, whatever a person's accept made of them), the model features only those files implemented, and every edge touching them. Denied evidence
/// is struck from what stays. The same decision then cuts the stored graph and the overrides (<see cref="MapAccess.View"/> applies the rest to the cut graph), so no reader
/// sees a graph the overrides were applied to before the cut.
/// </summary>
internal sealed class DenyCut
{
    private readonly IReadOnlyList<ContextRepo> _repos;
    private readonly IReadOnlyList<ContextRepo> _mapped;
    private readonly HashSet<string> _removed;
    private readonly HashSet<string> _cutLinks;
    private readonly Dictionary<string, List<GraphNode>> _origin;
    private readonly Dictionary<string, string> _aliases;
    private readonly HashSet<string> _stored;
    private readonly HashSet<string> _added;
    private readonly HashSet<string> _links;

    private DenyCut(IReadOnlyList<ContextRepo> repos, IReadOnlyList<ContextRepo> mapped, (HashSet<string> Removed, HashSet<string> CutLinks, HashSet<string> Links) cut, (Dictionary<string, List<GraphNode>> Origin, HashSet<string> Stored, HashSet<string> Added) nodes, Dictionary<string, string> aliases, bool any)
    {
        _repos = repos;
        _mapped = mapped;
        (_removed, _cutLinks, _links) = cut;
        (_origin, _stored, _added) = nodes;
        _aliases = aliases;
        Any = any;
    }

    /// <summary>Whether the globs take or strike anything from the graph this was judged on.</summary>
    public bool Any { get; }

    /// <summary>Judges the stored graph and the overrides together.</summary>
    /// <param name="stored">The graph as stored, before overrides.</param>
    /// <param name="overrides">Every override, a pending proposal too: what they add or remap is judged, so the decision does not depend on which reader asks.</param>
    /// <param name="repos">Every repo of the profile: their deny globs judge record keys and evidence refs, which name their repo.</param>
    /// <param name="mapped">The repos the map is built from: only their deny globs judge a <c xml:lang="en">code:</c> key, which names no repo (another repo's glob must not hide this repo's file).</param>
    /// <returns>The cut.</returns>
    public static DenyCut Of(GraphSnapshot stored, IReadOnlyList<MapOverride> overrides, IReadOnlyList<ContextRepo> repos, IReadOnlyList<ContextRepo> mapped)
    {
        var applied = overrides.Count == 0 ? stored : OverrideApplier.Apply(stored, overrides).Graph;
        var deniedDecisions = stored.Decisions.Where(d => DeniesKey(d.Record.Value, repos)).Select(d => d.ToKey().Value).ToHashSet(StringComparer.Ordinal);
        var origin = Origin(stored, overrides);
        var removed = origin.Where(p => IsCut(p.Value[0], deniedDecisions, repos, mapped)).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        removed.UnionWith(applied.Nodes.Where(n => IsDeniedKey(n.Key, repos, mapped)).Select(n => n.Key.Value));
        var edges = ReferenceEquals(applied, stored) ? stored.Edges : [.. stored.Edges, .. applied.Edges];
        removed.UnionWith(OrphanedFeatures(origin, stored, [.. edges.Where(e => IsCut(e, removed, repos))], removed));
        var cutLinks = edges.Where(e => IsCut(e, removed, repos)).Select(e => LinkIds.For(e).Value).ToHashSet(StringComparer.Ordinal);
        var any = removed.Count > 0 || cutLinks.Count > 0 || deniedDecisions.Count > 0
            || origin.Values.Any(nodes => nodes.Exists(n => HasDenied(n.EvidenceOrEmpty, repos))) || stored.Edges.Any(e => HasDenied(e.EvidenceOrEmpty, repos));
        var links = edges.Select(e => LinkIds.For(e).Value).ToHashSet(StringComparer.Ordinal);
        var aliases = stored.Aliases.ToDictionary(a => a.Alias.Value, a => a.Target.Value, StringComparer.Ordinal);
        return new DenyCut(repos, mapped, (removed, cutLinks, links), (origin, stored.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal), OverrideLog.Active(overrides).Where(o => o.Action == OverrideAction.Add).Select(o => o.Target.Value).ToHashSet(StringComparer.Ordinal)), aliases, any);
    }

    /// <summary>The evidence a deny glob leaves.</summary>
    /// <param name="evidence">The evidence, or null.</param>
    /// <returns>The same list when no glob denies any of it.</returns>
    public IReadOnlyList<EvidenceRef>? AllowedEvidence(IReadOnlyList<EvidenceRef>? evidence) => Allowed(evidence, _repos);

    /// <summary>The graph without what was cut: the nodes, the edges (by their endpoints or link), the aliases and decisions about them, and denied evidence struck from the rest.</summary>
    /// <param name="graph">The stored graph.</param>
    /// <returns>The graph itself when nothing is denied.</returns>
    public GraphSnapshot Apply(GraphSnapshot graph)
    {
        if (!Any)
        {
            return graph;
        }

        return graph with
        {
            Nodes = [.. graph.Nodes.Where(n => !_removed.Contains(n.Key.Value)).Select(n => n with { Evidence = Allowed(n.Evidence, _repos) })],
            Edges = [.. graph.Edges.Where(e => !_removed.Contains(e.From.Value) && !_removed.Contains(e.To.Value) && !_cutLinks.Contains(LinkIds.For(e).Value)).Select(e => e with { Evidence = Allowed(e.Evidence, _repos) })],
            Aliases = [.. graph.Aliases.Where(a => !_removed.Contains(a.Alias.Value) && !_removed.Contains(a.Target.Value) && !IsDeniedKey(a.Alias, _repos, _mapped) && !IsDeniedKey(a.Target, _repos, _mapped))],
            Decisions = [.. graph.Decisions.Where(d => !DeniesKey(d.Record.Value, _repos) && !_removed.Contains(d.ToKey().Value))],
        };
    }

    /// <summary>The overrides that still mean something: not about a removed node, a cut link or a denied path, a proposal without its denied evidence (or at all when that was all it had), and a reject that also names its proposal by the evidence the globs leave.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The overrides a caller may see.</returns>
    public IReadOnlyList<MapOverride> Apply(IReadOnlyList<MapOverride> overrides)
    {
        var kept = new List<MapOverride>(overrides.Count);
        var gone = GoneByCut(overrides);
        foreach (var o in overrides)
        {
            if (gone.Contains(o.ToId().Value) || About(o))
            {
                continue;
            }

            var row = o.Proposal is { Evidence: { Count: > 0 } evidence } payload && HasDenied(evidence, _repos) ? o with { Proposal = payload with { Evidence = Allowed(evidence, _repos) } } : o;
            kept.Add(row.Action == OverrideAction.Reject && row.Rejection is { } identity ? row with { AllowedRejection = AllowedIdentity(row.Target, identity) } : row);
        }

        return kept;
    }

    /// <summary>
    /// The decisions a person made about a node or link this cut takes out, which a promote or a build undoes with it: once the cut graph is stored their target is gone, and an accept, rename,
    /// move or merge about a gone node whose key no glob denies (a surface only a denied file evidenced) would be an orphan, shown in the queue and blocking the next promote. A merge or
    /// move whose other node is cut counts too. A reject or delete is not undone: it is satisfied once its target is gone and keeps the item rejected if the glob is removed. A decision about a
    /// key a glob denies outright is dropped on every read (<see cref="About"/>), and one about a node only a propose row adds goes with that row, so neither is undone.
    /// </summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The active accepts, renames, moves and merges about what is cut, targets matched through stored aliases.</returns>
    public IReadOnlyList<MapOverride> CutDecisions(IReadOnlyList<MapOverride> overrides) =>
        [.. OverrideLog.Active(overrides).Where(o => o.Action is OverrideAction.Accept or OverrideAction.Rename or OverrideAction.Move or OverrideAction.Merge && TakenOut(o))];

    /// <summary>The undo rows that take <see cref="CutDecisions"/> out, marked as the cut's (see <see cref="MapOverride.UndoingCut"/>).</summary>
    /// <param name="overrides">Every override.</param>
    /// <param name="at">When.</param>
    /// <returns>One undo row per decision.</returns>
    public IReadOnlyList<MapOverride> UndoRows(IReadOnlyList<MapOverride> overrides, DateTimeOffset at) => [.. CutDecisions(overrides).Select(o => MapOverride.UndoingCut(o, at))];

    private bool TakenOut(MapOverride o)
    {
        var (target, other) = (Resolve(o.Target.Value), o.Other is { } to ? Resolve(to.Value) : null);
        bool Stored(string key) => _stored.Contains(key) && _removed.Contains(key) && !IsDeniedKey(NodeKey.From(key), _repos, _mapped);
        return o.Link is { } link
            ? _cutLinks.Contains(link.Value) && !IsDeniedKey(NodeKey.From(target), _repos, _mapped)
            : Stored(target) || (other is not null && Stored(other) && !IsDeniedKey(NodeKey.From(target), _repos, _mapped));
    }

    private string Resolve(string key) => _aliases.TryGetValue(key, out var target) ? target : key;

    // The ids of the undo rows a cut wrote and of the rows they undo, while the node or link they were about is still cut or gone: no view shows either, so no name a denied file
    // produced is listed, exported or counted after the promote. If the glob goes and the node is back, both show again (the undo stays an undo).
    private HashSet<string> GoneByCut(IReadOnlyList<MapOverride> overrides)
    {
        var gone = new HashSet<string>(StringComparer.Ordinal);
        if (!overrides.Any(o => o.IsCutUndo))
        {
            return gone;
        }

        var byId = overrides.GroupBy(o => o.ToId().Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var undo in overrides.Where(o => o.IsCutUndo))
        {
            if (undo.Undoes is { } undone && byId.TryGetValue(undone.Value, out var row) && !Present(row))
            {
                _ = gone.Add(undo.ToId().Value);
                _ = gone.Add(undone.Value);
            }
        }

        return gone;
    }

    // Whether what a decision is about is back in the map the globs leave (not removed, and known to the stored graph or a propose row).
    private bool Present(MapOverride row) =>
        row.Link is { } link
            ? _links.Contains(link.Value) && !_cutLinks.Contains(link.Value)
            : Alive(row.Target) && (row.Other is not { } other || Alive(other));

    private bool Alive(NodeKey key) => Known(key) && Resolve(key.Value) is var k && !_removed.Contains(k) && !IsDeniedKey(NodeKey.From(k), _repos, _mapped);

    // Every form a node was first seen in: as stored, and as each active propose row would add it. A reject names its proposal by the evidence it had then.
    private static Dictionary<string, List<GraphNode>> Origin(GraphSnapshot stored, IReadOnlyList<MapOverride> overrides)
    {
        var origin = new Dictionary<string, List<GraphNode>>(StringComparer.Ordinal);
        foreach (var n in stored.Nodes.Concat(OverrideLog.Active(overrides).Where(o => o.Action == OverrideAction.Propose).Select(ProposalRows.NodeOf).OfType<GraphNode>()))
        {
            if (origin.TryGetValue(n.Key.Value, out var forms))
            {
                forms.Add(n);
            }
            else
            {
                origin[n.Key.Value] = [n];
            }
        }

        return origin;
    }

    // The identity a reject written before the glob (hashing the full evidence) has once the node's denied evidence is struck.
    private string? AllowedIdentity(NodeKey target, string identity)
    {
        var key = _aliases.TryGetValue(target.Value, out var resolved) ? resolved : target.Value;
        if (!_origin.TryGetValue(key, out var forms))
        {
            return null;
        }

        var match = forms.Find(n => HasDenied(n.EvidenceOrEmpty, _repos) && RejectionIdentity.For(n) == identity);
        return match is null ? null : RejectionIdentity.For(match with { Evidence = Allowed(match.Evidence, _repos) });
    }

    private bool About(MapOverride o) =>
        Removed(o.Target) || (o.Other is { } other && Removed(other))
        || (o.Link is { } link && _cutLinks.Contains(link.Value))
        || (o.Proposal is { Evidence: { Count: > 0 } evidence } && OnlyDenied(evidence, _repos))
        || (o.Action is OverrideAction.Reject or OverrideAction.Delete && o.Link is null && !Known(o.Target));

    // A node the stored graph, a propose row or an add row knows. A reject or delete of one nothing knows is satisfied (the cut took its node out): it stays in the log, so the item
    // stays rejected if the node comes back, and no view shows it while it is gone.
    private bool Known(NodeKey key) => Resolve(key.Value) is var k && (_origin.ContainsKey(k) || _added.Contains(k));

    // Removed by the cut or denied outright, by the key or through a stored alias of it.
    private bool Removed(NodeKey key) => _removed.Contains(key.Value) || IsDeniedKey(key, _repos, _mapped) || (Resolve(key.Value) is var k && k != key.Value && (_removed.Contains(k) || IsDeniedKey(NodeKey.From(k), _repos, _mapped)));

    // A node the globs take: a denied path's code node, a denied document's record node, a decision quoting one, or a node a model or a scan made on denied files alone
    // (a person's accept doesn't give the file back: this judges the node as it was first seen).
    private static bool IsCut(GraphNode n, HashSet<string> deniedDecisions, IReadOnlyList<ContextRepo> repos, IReadOnlyList<ContextRepo> mapped) =>
        IsDeniedKey(n.Key, repos, mapped) || deniedDecisions.Contains(n.Key.Value) || (n.Provenance != Provenance.Human && OnlyDenied(n.EvidenceOrEmpty, repos));

    private static bool IsCut(GraphEdge e, HashSet<string> removed, IReadOnlyList<ContextRepo> repos) =>
        removed.Contains(e.From.Value) || removed.Contains(e.To.Value) || (e.Provenance != Provenance.Human && OnlyDenied(e.EvidenceOrEmpty, repos)) || DeniedRollUp(e, repos);

    // A roll-up edge (a count) is summed over files, so it can't be recomputed per file: it goes as soon as any file it cites is denied, a 'depends-on' pair's imported file (the span) included.
    private static bool DeniedRollUp(GraphEdge e, IReadOnlyList<ContextRepo> repos) =>
        e.Count is not null && e.EvidenceOrEmpty.Any(r => Denies(r, repos) || (r.Span is { } target && target.StartsWith("repo:", StringComparison.Ordinal) && DeniesKey(target, repos)));

    private static bool IsDeniedKey(NodeKey key, IReadOnlyList<ContextRepo> repos, IReadOnlyList<ContextRepo> mapped) =>
        (key.Kind == NodeKind.Code && mapped.Any(r => r.Denies(key.Value["code:".Length..]))) || (key.Kind == NodeKind.Record && DeniesKey(key.Value["record:".Length..], repos));

    // The derived features left with no file link once the cut edges are gone, accepted or not: a person accepting a model feature doesn't give its denied file back. Derived means made by the build:
    // a model feature (a cluster's or a proposal's), a tag feature, a home feature, and a group feature all of whose child features are gone. Only stored 'implements' and 'tags' edges count (a merge's remapped edge goes
    // with the merge, which is dropped when its node is cut). A feature a person wrote stays.
    private static HashSet<string> OrphanedFeatures(Dictionary<string, List<GraphNode>> origin, GraphSnapshot stored, List<GraphEdge> cut, HashSet<string> removed)
    {
        static bool FileLink(GraphEdge e) => e.Type is EdgeType.Implements or EdgeType.Tags;
        var cutSet = cut.ToHashSet();
        var stillLinked = stored.Edges.Where(e => FileLink(e) && !cutSet.Contains(e)).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        bool Derived(string feature) => origin.TryGetValue(feature, out var forms) && forms[0] is { Provenance: Provenance.Model or Provenance.Deterministic, Key.Kind: NodeKind.Feature } && !removed.Contains(feature);
        var orphans = cut.Where(FileLink).Select(e => e.To.Value).Where(feature => Derived(feature) && !stillLinked.Contains(feature)).ToHashSet(StringComparer.Ordinal);
        AddEmptiedGroups(origin, stored, removed, orphans, stillLinked);
        return orphans;
    }

    // A group feature (deterministic, holding no file) all of whose child features are gone goes too, up the chain.
    private static void AddEmptiedGroups(Dictionary<string, List<GraphNode>> origin, GraphSnapshot stored, HashSet<string> removed, HashSet<string> orphans, HashSet<string> stillLinked)
    {
        var children = stored.Edges.Where(e => e is { Type: EdgeType.PartOf, From.Kind: NodeKind.Feature, To.Kind: NodeKind.Feature }).ToLookup(e => e.To.Value, e => e.From.Value, StringComparer.Ordinal);
        bool Emptied(string group) =>
            !removed.Contains(group) && !orphans.Contains(group) && !stillLinked.Contains(group)
            && origin.TryGetValue(group, out var forms) && forms[0] is { Provenance: Provenance.Deterministic, Key.Kind: NodeKind.Feature }
            && children[group].All(c => removed.Contains(c) || orphans.Contains(c));
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var group in children.Select(g => g.Key).Where(Emptied).ToList())
            {
                grew |= orphans.Add(group);
            }
        }
    }

    // A record key such as 'repo:work-app/private/notes.md' is denied when the repo it names denies the path after the repo.
    private static bool DeniesKey(string recordKey, IReadOnlyList<ContextRepo> repos) =>
        repos.Any(r => recordKey.StartsWith($"repo:{r.Name.Value}/", StringComparison.Ordinal) && r.Denies(recordKey[$"repo:{r.Name.Value}/".Length..]));

    private static bool Denies(EvidenceRef reference, IReadOnlyList<ContextRepo> repos) => DeniesKey(reference.Key.Value, repos);

    private static bool HasDenied(IReadOnlyList<EvidenceRef> evidence, IReadOnlyList<ContextRepo> repos) => evidence.Any(r => Denies(r, repos));

    private static bool OnlyDenied(IReadOnlyList<EvidenceRef> evidence, IReadOnlyList<ContextRepo> repos) => evidence.Count > 0 && evidence.All(r => Denies(r, repos));

    private static IReadOnlyList<EvidenceRef>? Allowed(IReadOnlyList<EvidenceRef>? evidence, IReadOnlyList<ContextRepo> repos) =>
        evidence is null || !HasDenied(evidence, repos) ? evidence : [.. evidence.Where(r => !Denies(r, repos))];
}
