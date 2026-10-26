using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#1043 ask 1: when the map rules change, an edge whose only support is an engine accept is decided again under the new rules, not carried.</summary>
public sealed class BuildMapRulesRedecideTests : IDisposable
{
    private const string ProductFile = "src/billing/invoice.ts";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope ChangedRules() =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo,
            [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DemotePaths = ["**/unrelated/**"] }]);

    private async Task<MapOverride> BuildAcceptedPromotedMap(string who)
    {
        _world.Clusters.Set = new ClusterSet([new FileCluster(ClusterId.From("c1"), [ProductFile, "src/billing/tax.ts"], 0.7)], Available: true);
        var first = (await _world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None)).Value;
        var built = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var edge = built.Graph.Edges.Single(e => e.Type == EdgeType.Implements && e.From.Value == "code:" + ProductFile);
        var accept = new MapOverride(OverrideAction.Accept, edge.From, who, _world.Clock.GetUtcNow(), Link: LinkIds.For(edge));
        _ = await _world.Store.AppendOverrides([accept], MapWorld.None);
        _ = await _world.Store.Promote(first.Draft.Number, MapWorld.None);
        return accept;
    }

    private async Task<(bool Kept, IReadOnlyList<MapOverride> Log)> Rebuild(ContextScope scope, bool fileStillFound)
    {
        _world.Clusters.Set = fileStillFound
            ? new ClusterSet([new FileCluster(ClusterId.From("c1"), [ProductFile, "src/billing/tax.ts"], 0.7)], Available: true)
            : new ClusterSet([], Available: true);
        _ = await _world.Map.BuildMap(progress: null, scope, MapWorld.None);
        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var log = (await _world.Store.Overrides(MapWorld.None)).Value;
        return (draft.Graph.Edges.Any(e => e.Type == EdgeType.Implements && e.From.Value == "code:" + ProductFile), log);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_engine_only_accepted_edge_the_new_rules_no_longer_find_is_dropped_and_its_accept_undone()
    {
        var accept = await BuildAcceptedPromotedMap("service:engine");

        var (kept, log) = await Rebuild(ChangedRules(), fileStillFound: false);

        Assert.False(kept);
        Assert.Contains(log, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_engine_accepted_edge_the_new_rules_find_again_is_kept_with_its_accept()
    {
        var accept = await BuildAcceptedPromotedMap("service:engine");

        var (kept, log) = await Rebuild(ChangedRules(), fileStillFound: true);

        Assert.True(kept);
        Assert.DoesNotContain(log, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_person_accepted_edge_the_new_rules_no_longer_find_is_kept()
    {
        var accept = await BuildAcceptedPromotedMap("joi");

        var (kept, log) = await Rebuild(ChangedRules(), fileStillFound: false);

        Assert.True(kept);
        Assert.DoesNotContain(log, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_engine_accepted_edge_is_kept_when_the_rules_did_not_change()
    {
        var accept = await BuildAcceptedPromotedMap("service:engine");

        var (kept, log) = await Rebuild(MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), fileStillFound: false);

        Assert.True(kept);
        Assert.DoesNotContain(log, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
    }
}
