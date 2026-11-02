using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Spec 2026-10-27 §2 through §6 step 3, end to end over the fake repo: homes, tags, the labeler's input, the purge and its idempotence.</summary>
public sealed class BuildMapHomeFeaturesTests : IDisposable
{
    private const string Home = "feature:aw-split-panes";
    private const string Tag = "feature:tag:aw-split-panes:src.commands";
    private static readonly string[] _homed = ["code:src/commands/ids.ts", "code:src/commands/registry.ts", "code:src/keys.ts", "code:src/ui/Split.tsx"];
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Scope => MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    private static ClusterSet Commands() => new([new FileCluster(ClusterId.From("c1"), ["src/commands/ids.ts", "src/commands/registry.ts"], 0.7)], Available: true);

    private async Task<GraphSnapshot> Build(ContextScope? scope = null)
    {
        _ = await _world.Map.BuildMap(progress: null, scope ?? Scope, MapWorld.None);
        return (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;
    }

    private static List<GraphEdge> Homes(GraphSnapshot graph) => [.. graph.Edges.Where(e => e.Type == EdgeType.Implements && e.To.Kind == NodeKind.Feature)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_gives_each_product_file_one_deterministic_home_feature_and_proposes_no_home_feature()
    {
        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;
        var graph = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;

        var homes = Homes(graph);
        Assert.Equal(_homed, homes.Select(e => e.From.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(homes, e => Assert.Equal((Home, Provenance.Deterministic), (e.To.Value, e.Provenance)));
        Assert.DoesNotContain(report.Proposed, p => p.Key.Kind == NodeKind.Feature);
        Assert.Contains(graph.Edges, e => e is { Type: EdgeType.PartOf, Provenance: Provenance.Deterministic } && e.From.Value == "surface:command/split-vertical" && e.To.Value == Home);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cluster_adds_a_tag_feature_with_model_tags_links_beside_the_home_and_never_a_second_home()
    {
        _world.Clusters.Set = Commands();

        var graph = await Build();

        Assert.Equal(_homed.Length, Homes(graph).Count);
        var tags = graph.Edges.Where(e => e.Type == EdgeType.Tags).ToList();
        Assert.Equal(["code:src/commands/ids.ts", "code:src/commands/registry.ts"], tags.Select(e => e.From.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(tags, e => Assert.Equal((Tag, Provenance.Model), (e.To.Value, e.Provenance)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_labeler_is_asked_about_home_groups_by_path_and_names_the_home_feature_not_the_tag()
    {
        _world.Clusters.Set = Commands();

        var graph = await Build(MapWorld.HumanWith("""{"repos":["aw-split-panes"],"labeling":{"enabled":true}}"""));

        var shown = Assert.Single(_world.Labeler.Shown);
        Assert.Equal(["src/commands/ids.ts", "src/commands/registry.ts", "src/keys.ts", "src/ui/Split.tsx"], shown.Files, StringComparer.Ordinal);
        Assert.Equal("Labeled " + shown.Id.Value, graph.Nodes.Single(n => n.Key.Value == Home).Label);
        Assert.Equal("Commands", graph.Nodes.Single(n => n.Key.Value == Tag).Label);
    }

    private async Task SeedOldStyleMap(MapOverride? accept = null)
    {
        var old = new GraphBuilder()
            .Node("feature:src.commands", "Commands", Provenance.Model, 0.7)
            .Node("code:src/commands/ids.ts", provenance: Provenance.Deterministic)
            .Edge("code:src/commands/ids.ts", EdgeType.Implements, "feature:src.commands", provenance: Provenance.Model, confidence: 0.7, evidence: "repo:aw-split-panes/src/commands/ids.ts")
            .Build();
        var saved = (await _world.Store.SaveDraft(new MapImport(ContextRepoName.From(FixtureMap.Repo), MapWorld.Head, old, accept is null ? [] : [accept]), _world.Clock.GetUtcNow(), MapWorld.None)).Value;
        _ = await _world.Store.Promote(saved.Number, MapWorld.None);
    }

    private MapOverride AcceptOldLink()
    {
        var edge = new GraphEdge(NodeKey.From("code:src/commands/ids.ts"), NodeKey.From("feature:src.commands"), EdgeType.Implements, Provenance.Model);
        return new MapOverride(OverrideAction.Accept, edge.From, "joi", _world.Clock.GetUtcNow(), Link: LinkIds.For(edge));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_over_an_old_style_map_replaces_the_cluster_feature_and_carries_an_accepted_link_to_the_tags_link()
    {
        var accept = AcceptOldLink();
        await SeedOldStyleMap(accept);
        _world.Clusters.Set = Commands();

        var graph = await Build();
        var log = (await _world.Store.Overrides(MapWorld.None)).Value;

        Assert.DoesNotContain(graph.Nodes, n => n.Key.Value == "feature:src.commands");
        Assert.Equal(1, Homes(graph).Count(e => e.From.Value == "code:src/commands/ids.ts"));
        var tag = graph.Edges.Single(e => e.Type == EdgeType.Tags && e.From.Value == "code:src/commands/ids.ts");
        Assert.Contains(log, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
        Assert.Contains(log, o => o.Action == OverrideAction.Accept && o.Link?.Value == LinkIds.For(tag).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_build_over_the_first_changes_no_edge_and_adds_no_decision()
    {
        await SeedOldStyleMap(AcceptOldLink());
        _world.Clusters.Set = Commands();
        var first = await Build();
        var firstDraft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        _ = await _world.Store.Promote(firstDraft.Version.Number, MapWorld.None);
        var logBefore = (await _world.Store.Overrides(MapWorld.None)).Value.Count;

        var second = await Build();

        Assert.Equal(first.Edges.Select(e => (e.From.Value, e.To.Value, e.Type)).Order(), second.Edges.Select(e => (e.From.Value, e.To.Value, e.Type)).Order());
        Assert.Equal(first.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), second.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(logBefore, (await _world.Store.Overrides(MapWorld.None)).Value.Count);
    }
}
