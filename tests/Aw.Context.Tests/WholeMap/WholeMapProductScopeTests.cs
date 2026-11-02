using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.WholeMap;

/// <summary>
/// Served-level regression for #1565 defect B: a repo-scoped whole-map answer (the GetWholeMap service through RepoAddressing and
/// GraphSnapshot.ScopedTo into the whole-map builder) counts and locates the product nodes attached to the repo by
/// 'on' and 'reads'/'writes' edges (spec §3 "and their cascade") — for a human naming the repo on a multi-repo profile and for a session
/// caller — and keeps another repo's actions and things out.
/// </summary>
public sealed class WholeMapProductScopeTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task Import()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:f\", label: \"F\"}",
            "  - {key: \"surface:screen/s\", label: \"S screen\"}",
            "  - {key: \"code:src/s.ts\", label: \"s.ts\", provenance: deterministic}",
            "  - {key: \"action:s/do-it\", label: \"Do it\"}",
            "  - {key: \"thing:s-state\", label: \"S state\"}",
            "  - {key: \"surface:screen/o\", label: \"O screen\"}",
            "  - {key: \"code:src/o.ts\", label: \"o.ts\", provenance: deterministic}",
            "  - {key: \"action:o/do-it\", label: \"Do the other\"}",
            "  - {key: \"thing:o-state\", label: \"O state\"}",
            "edges:",
            "  - {from: \"surface:screen/s\", to: \"feature:f\", type: part-of}",
            "  - {from: \"code:src/s.ts\", to: \"surface:screen/s\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/s.ts\"}]}",
            "  - {from: \"action:s/do-it\", to: \"surface:screen/s\", type: \"on\"}",
            "  - {from: \"code:src/s.ts\", to: \"thing:s-state\", type: writes}",
            "  - {from: \"code:src/o.ts\", to: \"surface:screen/o\", type: implements, provenance: deterministic, evidence: [{key: \"repo:other/src/o.ts\"}]}",
            "  - {from: \"action:o/do-it\", to: \"surface:screen/o\", type: \"on\"}",
            "  - {from: \"code:src/o.ts\", to: \"thing:o-state\", type: writes}"));
    }

    private ValueTask<Kernel.Result<WholeMapView>> Ask(ContextScope? scope = null, string? repo = null, string[]? locate = null) =>
        _world.Engine.WholeMap(new WholeMapQuery(Detail: null, locate ?? [], MapRead.Current, repo), scope ?? MapWorld.Human, MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task The_served_whole_map_counts_the_repos_actions_and_things_and_locates_them()
    {
        await Import();

        var view = (await Ask(repo: "aw-split-panes", locate: ["thing:s-state", "thing:o-state"])).Value;

        Assert.Equal("aw-split-panes", view.Repo);
        Assert.Equal((1, 1), (view.Counts.ByKind["action"], view.Counts.ByKind["thing"]));   // not 2: the other repo's action and thing stay out
        Assert.Equal(0, view.Counts.ByKind["flow"]);   // no flow nodes exist in any map yet (defect A of #1565, follow-up work)

        // locate resolves the served node (its area is the unassigned group: a thing votes through its reads/writes sources and code
        // settles only after things, AreaAssigner) while the other repo's thing is not the session's to place.
        Assert.NotNull(Assert.Single(view.Located!, l => l.Key == "thing:s-state").Area);
        Assert.Null(Assert.Single(view.Located!, l => l.Key == "thing:o-state").Area);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sessions_served_whole_map_counts_them_too()
    {
        await Import();

        var view = (await Ask(scope: MapWorld.Session("aw-split-panes"), locate: ["thing:s-state", "thing:o-state"])).Value;

        Assert.Equal((1, 1), (view.Counts.ByKind["action"], view.Counts.ByKind["thing"]));
        Assert.NotNull(Assert.Single(view.Located!, l => l.Key == "thing:s-state").Area);
        Assert.Null(Assert.Single(view.Located!, l => l.Key == "thing:o-state").Area);
    }
}
