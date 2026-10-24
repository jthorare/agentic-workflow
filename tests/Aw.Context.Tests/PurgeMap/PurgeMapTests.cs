using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.PurgeMap;

public sealed class PurgeMapTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_deletes_expired_exports_and_versions_past_the_last_100_and_keeps_the_rest()
    {
        for (var i = 0; i < MapRetention.RetainedVersions + 3; i++)
        {
            _ = await _world.ImportFixture();
        }

        _world.Clock.Advance(TimeSpan.FromDays(91));   // past the eval window: nothing a backtest reads is among the surplus
        var old = (await _world.Map.ExportMap("old.json", MapWorld.Human, MapWorld.None)).Value.Path;
        var fresh = (await _world.Map.ExportMap("fresh.json", MapWorld.Human, MapWorld.None)).Value.Path;
        _world.Exports.Touch(old, _world.Clock.GetUtcNow().AddDays(-14).AddSeconds(-1));
        _world.Exports.Touch(fresh, _world.Clock.GetUtcNow().AddDays(-14));

        var report = (await _world.Map.PurgeExpired(MapWorld.None)).Value;

        Assert.Equal(new PurgeReport(Exports: 1, Versions: 2), report);
        Assert.Equal(fresh, Assert.Single(_world.Exports.Documents.Keys));
        var versions = (await _world.Store.Versions(MapWorld.None)).Value;
        Assert.Equal(MapRetention.RetainedVersions + 1, versions.Count);
        Assert.Equal(MapRetention.RetainedVersions + 3, versions[0].Number);
        Assert.Equal(MapHealthState.Ok, (await _world.Map.CheckRetention(MapWorld.None)).Value.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_keeps_every_version_that_was_current_within_the_eval_window_even_past_the_newest_100()
    {
        var imports = MapRetention.RetainedVersions + 10;
        for (var i = 0; i < imports; i++)
        {
            _ = await _world.ImportFixture();
            _world.Clock.Advance(TimeSpan.FromHours(12));
        }

        var report = (await _world.Map.PurgeExpired(MapWorld.None)).Value;

        Assert.Equal(0, report.Versions);
        Assert.Equal(imports, (await _world.Store.Versions(MapWorld.None)).Value.Count);
        Assert.Equal(MapHealthState.Ok, (await _world.Map.CheckRetention(MapWorld.None)).Value.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_drops_versions_whose_successor_is_older_than_the_eval_window_once_past_the_newest_100()
    {
        for (var i = 0; i < MapRetention.RetainedVersions + 10; i++)
        {
            _ = await _world.ImportFixture();
            if (i < 30)
            {
                _world.Clock.Advance(TimeSpan.FromDays(1));   // only the first 30 imports are a day apart; the surplus v1..v10 sit among them
            }
        }

        _world.Clock.Advance(TimeSpan.FromDays(75));   // v1..v9 were replaced more than 90 days ago, v10 and later less

        var report = (await _world.Map.PurgeExpired(MapWorld.None)).Value;

        Assert.Equal(9, report.Versions);
        Assert.Equal(MapRetention.RetainedVersions + 10 - 9, (await _world.Store.Versions(MapWorld.None)).Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_keeps_a_surplus_version_whose_successor_is_inside_the_window_however_old_the_version_itself_is()
    {
        _ = await _world.ImportFixture();   // v1, created today
        _world.Clock.Advance(TimeSpan.FromDays(100));
        for (var i = 0; i < MapRetention.RetainedVersions + 2; i++)
        {
            _ = await _world.ImportFixture();   // v2.. within a minute of each other
        }

        _world.Clock.Advance(TimeSpan.FromDays(10));   // v1 is 110 days old but was current until v2, 10 days ago: a backtest of an issue from those days reads it

        var report = (await _world.Map.PurgeExpired(MapWorld.None)).Value;

        Assert.Equal(0, report.Versions);
        Assert.Equal(MapRetention.RetainedVersions + 3, (await _world.Store.Versions(MapWorld.None)).Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Retention_health_ok_text_does_not_claim_at_most_100_versions_when_the_eval_window_keeps_more()
    {
        for (var i = 0; i < MapRetention.RetainedVersions + 10; i++)
        {
            _ = await _world.ImportFixture();
            _world.Clock.Advance(TimeSpan.FromHours(12));
        }

        var health = (await _world.Map.CheckRetention(MapWorld.None)).Value;

        Assert.Equal(MapHealthState.Ok, health.State);
        Assert.DoesNotContain("at most 100", health.Detail, StringComparison.Ordinal);   // every version is kept: each was current within the 90-day window
        Assert.Contains("90-day", health.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Purge_with_nothing_to_do_reports_zero() =>
        Assert.Equal(new PurgeReport(Exports: 0, Versions: 0), (await _world.Map.PurgeExpired(MapWorld.None)).Value);
}
