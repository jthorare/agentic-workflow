using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SyncSources;

/// <summary>The index tick keeps the manual sync's skips (#1574): no map yet, the feeds still sync with no pass; a live intent source keeps its decisions from the walk.</summary>
[Trait("Lane", "unit")]
public sealed class TickDecisionsSkipTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Issue(DateTimeOffset now) => new(
        RecordKey.From("github:issue/acme/shop/7"), RecordKind.Issue, "Pane resize limits", "Pane resize: we agreed the minimum pane width is 20 columns.", Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("repo:acme/shop"));

    private static DateTimeOffset Now => DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);   // MapWorld's clock

    [Fact]
    public async Task Tick_syncs_the_feeds_when_there_is_no_map_yet()
    {
        using var world = new MapWorld(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, [Issue(Now)])));

        var report = await world.Engine.Tick(MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}"), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        _ = Assert.Single((await world.Records.List(new RecordFilter(), after: null, 10, Ct)).Value);
        Assert.Empty((await world.Intents.Layer(Ct)).Value.Decisions);
        Assert.Empty(world.Model.Requests);
    }

    [Fact]
    public async Task Tick_does_not_read_a_live_intent_sources_feed_for_decisions()
    {
        var issue = Issue(Now);
        var walk = new FakeContextSource(new SourceDescriptor(RecordSamples.Github, SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false), []);
        using var world = new MapWorld(intent: walk, feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, [issue])));
        _ = await world.ImportFixture();

        var report = await world.Engine.Tick(MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}"), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        _ = Assert.Single((await world.Records.List(new RecordFilter(), after: null, 10, Ct)).Value);   // the record did sync
        Assert.DoesNotContain((await world.Intents.Layer(Ct)).Value.Records, r => r.Key == issue.Key);
        Assert.Empty(world.Model.Requests);
    }
}
