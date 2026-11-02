using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.WholeMap;

/// <summary>
/// Served-level regression for #1565 defect B: a repo-scoped whole-map answer keeps the repo's data-access code files (they implement
/// no surface, so only their file evidence ties them to the repo) and the things those files write, for a human naming the repo and
/// for a session caller, and keeps another repo's data-access file and thing out.
/// </summary>
public sealed class WholeMapDataAccessScopeTests : IDisposable
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
            "  - {key: \"surface:screen/s\", label: \"S screen\"}",
            "  - {key: \"code:src/s.ts\", label: \"s.ts\", provenance: deterministic}",
            "  - {key: \"code:src/dal.ts\", label: \"dal.ts\", provenance: deterministic}",
            "  - {key: \"thing:account\", label: \"Account\", evidence: [{key: \"repo:aw-split-panes/src/dal.ts\"}]}",
            "  - {key: \"surface:screen/o\", label: \"O screen\"}",
            "  - {key: \"code:src/o.ts\", label: \"o.ts\", provenance: deterministic}",
            "  - {key: \"code:src/odal.ts\", label: \"odal.ts\", provenance: deterministic}",
            "  - {key: \"thing:ledger\", label: \"Ledger\", evidence: [{key: \"repo:other/src/odal.ts\"}]}",
            "edges:",
            "  - {from: \"code:src/s.ts\", to: \"surface:screen/s\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/s.ts\"}]}",
            "  - {from: \"code:src/dal.ts\", to: \"thing:account\", type: writes, evidence: [{key: \"repo:aw-split-panes/src/dal.ts\"}]}",
            "  - {from: \"code:src/o.ts\", to: \"surface:screen/o\", type: implements, provenance: deterministic, evidence: [{key: \"repo:other/src/o.ts\"}]}",
            "  - {from: \"code:src/odal.ts\", to: \"thing:ledger\", type: writes, evidence: [{key: \"repo:other/src/odal.ts\"}]}"));
    }

    private ValueTask<Kernel.Result<WholeMapView>> Ask(ContextScope? scope = null, string? repo = null, string[]? locate = null) =>
        _world.Engine.WholeMap(new WholeMapQuery(Detail: null, locate ?? [], MapRead.Current, repo), scope ?? MapWorld.Human, MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_scoped_read_keeps_the_repos_data_access_file_and_the_thing_it_writes()
    {
        await Import();

        var view = (await Ask(repo: "aw-split-panes", locate: ["thing:account", "thing:ledger"])).Value;

        Assert.Equal(1, view.Counts.ByKind["thing"]);
        Assert.Equal(2, view.Counts.ByKind["code"]);   // s.ts and dal.ts, not the other repo's two
        Assert.NotNull(Assert.Single(view.Located!, l => l.Key == "thing:account").Area);
        Assert.Null(Assert.Single(view.Located!, l => l.Key == "thing:ledger").Area);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sessions_repo_scoped_read_keeps_the_data_access_file_and_the_thing_too()
    {
        await Import();

        var view = (await Ask(scope: MapWorld.Session("aw-split-panes"), locate: ["thing:account", "thing:ledger"])).Value;

        Assert.Equal(1, view.Counts.ByKind["thing"]);
        Assert.Equal(2, view.Counts.ByKind["code"]);
        Assert.NotNull(Assert.Single(view.Located!, l => l.Key == "thing:account").Area);
        Assert.Null(Assert.Single(view.Located!, l => l.Key == "thing:ledger").Area);
    }
}
