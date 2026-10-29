using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SyncSources;

/// <summary>A source the intent walk already reads keeps its decisions from that walk: its record feed gets no decision pass of its own (#1373).</summary>
[Trait("Lane", "unit")]
public sealed class RecordDecisionsSkipTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Issue(DateTimeOffset now) => new(
        RecordKey.From("github:issue/acme/shop/7"), RecordKind.Issue, "Pane resize limits", "Pane resize: we agreed the minimum pane width is 20 columns.", Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("repo:acme/shop"));

    [Fact]
    public async Task Feeds_sync_when_there_is_no_map_yet()
    {
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);   // MapWorld's clock
        using var world = new MapWorld(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, [Issue(now)])));

        var report = await world.Engine.Sync(source: null, MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}"), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        _ = Assert.Single((await world.Records.List(new RecordFilter(), after: null, 10, Ct)).Value);
        Assert.Empty((await world.Intents.Layer(Ct)).Value.Decisions);
        Assert.Empty(world.Model.Requests);
    }

    [Fact]
    public async Task A_feed_whose_source_also_has_an_intent_walk_is_not_read_for_decisions_twice()
    {
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);   // MapWorld's clock
        var issue = Issue(now);
        var walk = new FakeContextSource(new SourceDescriptor(RecordSamples.Github, SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false), []);
        using var world = new MapWorld(intent: walk, feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, [issue])));
        _ = await world.ImportFixture();

        var report = await world.Engine.Sync(source: null, MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}"), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        _ = Assert.Single((await world.Records.List(new RecordFilter(), after: null, 10, Ct)).Value);   // the record did sync
        Assert.DoesNotContain((await world.Intents.Layer(Ct)).Value.Records, r => r.Key == issue.Key);
        Assert.Empty(world.Model.Requests);
    }

    private const string Quote = "the minimum pane width is 20 columns";
    private const string Resize = "feature:layout.pane-resize";

    private static ModelPurpose Extraction => ModelPurpose.DecisionExtraction;

    private static async Task<MapWorld> Rig()
    {
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);   // MapWorld's clock
        var item = Issue(now) with { Text = $"Pane resize: we agreed {Quote}, anything narrower is clamped." };
        var world = new MapWorld(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, [item])));
        _ = await world.ImportFixture();
        world.Model.Answer = $$"""{"decisions":[{"quote":"{{Quote}}","wording":"Panes are at least 20 columns wide","decides":["{{Resize}}"]}]}""";
        return world;
    }

    private static async Task Sync(MapWorld world)
    {
        var report = await world.Engine.Sync(source: null, MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}"), Ct);
        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
    }

    /// <summary>Writes the stored issue again with a new source version and text, as the feed would on its next pull.</summary>
    private static async Task StoreNewVersion(MapWorld world)
    {
        var stored = (await world.Records.List(new RecordFilter(Source: RecordSamples.Github), after: null, 10, Ct)).Value.Single();
        var text = $"Pane resize: we agreed {Quote}, and the maximum is 80 columns.";
        var position = (await world.Records.Position(RecordSamples.Github, Ct)).Value;
        var changed = stored with { Version = SourceVersion.From("v2"), TextHash = RecordSamples.Hash(text) };
        var committed = await world.Records.Commit(new RecordBatch(RecordSamples.Github, [RecordSamples.Write(changed, text)], [], position, world.Clock.GetUtcNow()), Ct);
        Assert.True(committed.IsSuccess, committed.IsError ? committed.Error.Message : null);
    }

    [Fact]
    public async Task An_unchanged_record_is_not_sent_to_the_model_on_the_next_sync()
    {
        using var world = await Rig();

        await Sync(world);
        await Sync(world);

        _ = Assert.Single(world.Model.Requests, r => r.Purpose == Extraction);
    }

    [Fact]
    public async Task A_map_rebuild_does_not_send_unchanged_records_to_the_model_again()
    {
        using var world = await Rig();
        await Sync(world);
        var before = (await world.Map.MapStatus(anchor: null, MapWorld.Human, Ct)).Value.Version.Number;

        var second = await world.ImportYaml(MapWorld.Yaml("schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", $"  - {{key: \"{Resize}\", label: \"Pane resize\"}}"));
        Assert.NotEqual(before, second.Version.Number);
        await Sync(world);

        _ = Assert.Single(world.Model.Requests, r => r.Purpose == Extraction);
        _ = Assert.Single((await world.Intents.Layer(Ct)).Value.Decisions);
    }

    [Fact]
    public async Task A_changed_record_is_sent_to_the_model_again()
    {
        using var world = await Rig();
        await Sync(world);

        await StoreNewVersion(world);
        await Sync(world);

        Assert.Equal(2, world.Model.Requests.Count(r => r.Purpose == Extraction));
    }
}
