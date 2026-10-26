using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#1043: a standing accept must not carry a demoted file's 'implements' edge through a build; the build drops the edge and undoes the accept, as the deny cut does.</summary>
public sealed class BuildMapDemotedAcceptsTests : IDisposable
{
    private const string TestFile = "src/billing/__tests__/invoice.test.ts";
    private const string ProductFile = "src/billing/invoice.ts";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Demoting() =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo,
            [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DemotePaths = ["**/__tests__/**"] }]);

    private async Task<MapOverride> BuildAcceptedPromotedMap(string file, string who)
    {
        _world.Clusters.Set = new ClusterSet([new FileCluster(ClusterId.From("c1"), [TestFile, ProductFile], 0.7)], Available: true);
        var first = (await _world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None)).Value;
        var built = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var edge = built.Graph.Edges.Single(e => e.Type == EdgeType.Implements && e.From.Value == "code:" + file);
        var accept = new MapOverride(OverrideAction.Accept, edge.From, who, _world.Clock.GetUtcNow(), Link: LinkIds.For(edge));
        _ = await _world.Store.AppendOverrides([accept], MapWorld.None);
        _ = await _world.Store.Promote(first.Draft.Number, MapWorld.None);
        return accept;
    }

    private async Task<(IReadOnlyList<GraphEdge> Edges, IReadOnlyList<MapOverride> Log)> RebuildDemoting()
    {
        _ = await _world.Map.BuildMap(progress: null, Demoting(), MapWorld.None);
        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var log = (await _world.Store.Overrides(MapWorld.None)).Value;
        return ([.. draft.Graph.Edges.Where(e => e.Type == EdgeType.Implements)], log);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_person_accepted_implements_edge_from_a_demoted_file_is_dropped_by_the_next_build()
    {
        var accept = await BuildAcceptedPromotedMap(TestFile, "joi");

        var (edges, log) = await RebuildDemoting();

        Assert.DoesNotContain(edges, e => e.From.Value == "code:" + TestFile);
        Assert.Contains(log, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_engine_accepted_implements_edge_from_a_demoted_file_is_dropped_by_the_next_build()
    {
        var accept = await BuildAcceptedPromotedMap(TestFile, "service:engine");

        var (edges, log) = await RebuildDemoting();

        Assert.DoesNotContain(edges, e => e.From.Value == "code:" + TestFile);
        Assert.Contains(log, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_product_files_accepted_implements_edge_is_kept()
    {
        var accept = await BuildAcceptedPromotedMap(ProductFile, "joi");

        var (edges, log) = await RebuildDemoting();

        Assert.Contains(edges, e => e.From.Value == "code:" + ProductFile);
        Assert.DoesNotContain(log, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
    }
}
