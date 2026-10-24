using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.MapHealth;

public sealed class MapHealthTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Context_map_warns_when_there_is_no_map()
    {
        var check = (await _world.Map.CheckMap(scope: null, MapWorld.None)).Value;

        Assert.Equal(("context:map", MapHealthState.Warn), (check.Name, check.State));
        Assert.Contains("no map", check.Detail, StringComparison.Ordinal);
        Assert.Contains("context map import", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_map_is_ok_with_a_current_map()
    {
        _ = await _world.ImportFixture();

        var check = (await _world.Map.CheckMap(scope: null, MapWorld.None)).Value;

        Assert.Equal(MapHealthState.Ok, check.State);
        Assert.Equal("map v1 current (aw-split-panes, whole map: 20 nodes, 21 edges); commits behind HEAD: unknown until a build records its commit", check.Detail);
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_map_counts_the_map_as_status_does_once_a_deny_glob_hides_a_node()
    {
        _ = await _world.ImportFixture();
        var denying = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "src/**"] }]);

        var check = (await _world.Map.CheckMap(denying, MapWorld.None)).Value;
        var status = (await _world.Map.MapStatus(anchor: null, denying, MapWorld.None)).Value;

        Assert.Contains(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{status.Counts.Nodes} nodes, {status.Counts.Edges} edges"), check.Detail, StringComparison.Ordinal);
        Assert.NotEqual(20, status.Counts.Nodes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_map_fails_when_the_store_cant_open()
    {
        _world.Store.Failure = ContextErrors.StoreUnavailable.With("AW-DATA-001: locked");

        var check = (await _world.Map.CheckMap(scope: null, MapWorld.None)).Value;

        Assert.Equal(MapHealthState.Fail, check.State);
        Assert.Contains("SND-CONTEXT-004", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_retention_is_ok_when_nothing_is_overdue()
    {
        _ = await _world.ImportFixture();
        _ = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);

        var check = (await _world.Map.CheckRetention(MapWorld.None)).Value;

        Assert.Equal(("context:retention", MapHealthState.Ok), (check.Name, check.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_retention_warns_about_expired_exports_and_versions_past_the_last_100()
    {
        for (var i = 0; i < MapRetention.RetainedVersions + 2; i++)
        {
            _ = await _world.ImportFixture();
        }

        _world.Clock.Advance(TimeSpan.FromDays(91));
        var old = (await _world.Map.ExportMap("old.json", MapWorld.Human, MapWorld.None)).Value.Path;
        _world.Exports.Touch(old, _world.Clock.GetUtcNow().AddDays(-15));

        var check = (await _world.Map.CheckRetention(MapWorld.None)).Value;

        Assert.Equal(MapHealthState.Warn, check.State);
        Assert.Equal("1 export older than 14 days and 1 map version past the last 100 still present", check.Detail);
        Assert.Contains("context-purge", check.Fix, StringComparison.Ordinal);
    }
}
