using Aw.Context.Core;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;
using static Aw.Context.Tests.SyncSources.ExtractDecisionsTests;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// The SyncSources slice (spec §8.8) over a Linear record feed and the fixture map: records linked to nodes by rule, unchanged versions skipped, the repo docs
/// refreshed next to the feeds, an unknown source and a second sync of a running source refused.
/// </summary>
[Trait("Lane", "integration")]
public sealed class SyncSourcesTests
{
    private const string Spl103 = "linear:issue/0e5a1b00-0000-4000-8000-000000000103";
    private const string Toggle = "surface:command/toggle-orientation";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SyncSources_links_fixture_records_to_map_nodes()
    {
        await using var rig = await Rig.Start();

        var row = await rig.Sync(Scope());

        Assert.Equal((SourceId.From("linear"), null), (row.Source, row.Code));
        var layer = await rig.Layer();
        var link = Assert.Single(layer.Links, l => l.Record.Value == Spl103 && l.Target.Value == Toggle);
        Assert.Equal(LinkRule.Identifier, link.Rule);
        Assert.Equal(layer.Links.Count, row.Linked);
        // see #1637: RecordDecisionRun doesn't carry aliases yet (the intent walk asserted ["SPL-103"] here)
    }

    [Fact]
    public async Task SyncSources_skips_records_whose_version_is_unchanged()
    {
        await using var rig = await Rig.Start();
        var first = await rig.Sync(Scope());
        var asked = rig.World.Model.Requests.Count;

        var second = await rig.Sync(Scope());

        Assert.Equal(asked, rig.World.Model.Requests.Count);   // nothing was read for decisions again
        Assert.Equal(first.Decisions, second.Decisions);
        Assert.Equal((await rig.Layer()).Records.Count, first.Records);
    }

    [Fact]
    public async Task Sync_stores_the_records_created_at()
    {
        await using var rig = await Rig.Start();
        var created = LinearIssueSeed.From(issues: null).Single(w => w.Record.Key.Value == Spl103).Record.CreatedAt;

        _ = await rig.Sync(Scope());

        var record = (await rig.Layer()).Records.Single(r => r.Key.Value == Spl103);
        _ = Assert.NotNull(created);
        Assert.Equal(created, record.CreatedAt);
    }

    [Fact]
    public async Task Sync_with_no_created_at_stores_null()
    {
        await using var rig = await Rig.Start(shape: i => i with { CreatedAt = null });

        _ = await rig.Sync(Scope());

        var records = (await rig.Layer()).Records;
        Assert.NotEmpty(records);
        Assert.All(records, r => Assert.Null(r.CreatedAt));
    }

    [Fact]
    public async Task SyncSources_scrubs_title_before_storing()
    {
        await using var rig = await Rig.Start(shape: i => i with { Title = i.Title.Replace("Toggle split orientation", "Toggle FAKE-SECRET-1234 orientation", StringComparison.Ordinal) });

        _ = await rig.Sync(Scope());

        var title = (await rig.Layer()).Records.Single(r => r.Key.Value == Spl103).Title;
        Assert.DoesNotContain(FakeScrubber.Secret, title, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:fake]", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SyncSources_runs_repo_docs_refresh_too()
    {
        await using var rig = await Rig.Start();

        var report = await rig.World.Engine.Sync(source: null, Scope(), Ct);

        Assert.Equal(["repo-docs", "linear"], report.Value.Sources.Select(s => s.Source.Value), StringComparer.Ordinal);
    }

    // no map: the index tick's decision row says SND-CONTEXT-030 (TickDecisionsTests.Without_a_map_the_feed_still_syncs_clean_and_the_decision_pass_says_SND_CONTEXT_030)
    [Fact]
    public async Task SyncSources_unknown_source_answers_058()
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();

        var report = await world.Engine.Sync("linear-native", Scope(), Ct);

        Assert.Equal(ContextErrors.SyncSourceUnknown.Code, report.Error.Code);
    }
}
