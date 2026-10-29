using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// A tracker record synced through the owned record store (a <see cref="RecordFeed"/> run by <see cref="SyncSourcesService"/>) is read for decisions
/// like an Onyx record is: one that quotes a decision about a map feature it names becomes a decision node citing the record (#1373).
/// </summary>
[Trait("Lane", "unit")]
public sealed class RecordDecisionsTests
{
    private const string Quote = "the minimum pane width is 20 columns";
    private const string Resize = "feature:layout.pane-resize";
    private static readonly RecordKey _issue = RecordKey.From("github:issue/acme/shop/7");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Issue(DateTimeOffset now) => new(
        _issue, RecordKind.Issue, "Pane resize limits", $"Pane resize: we agreed {Quote}, anything narrower is clamped.", Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("repo:acme/shop"));

    private static string Section(bool labeling) => "{\"labeling\":{\"enabled\":" + (labeling ? "true" : "false") + "}}";

    private static async Task<MapWorld> Rig()
    {
        var clock = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);   // MapWorld's clock
        var world = new MapWorld(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, [Issue(clock)])));
        _ = await world.ImportFixture();
        world.Model.Answer = $$"""{"decisions":[{"quote":"{{Quote}}","wording":"Panes are at least 20 columns wide","decides":["{{Resize}}"]}]}""";
        return world;
    }

    [Fact]
    public async Task Tracker_record_quoting_a_decision_becomes_a_decision_citing_it()
    {
        using var world = await Rig();

        var report = await world.Engine.Sync(source: null, MapWorld.HumanWith(Section(labeling: true)), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        Assert.Equal(_issue.Value, Assert.Single((await world.Records.List(new RecordFilter(), after: null, 10, Ct)).Value).Key.Value);   // the record itself did sync
        var decisions = (await world.Intents.Layer(Ct)).Value.Decisions;
        var decision = Assert.Single(decisions);   // expected 1 decision citing the issue
        Assert.Equal(_issue, decision.Record);
        Assert.Equal(Quote, decision.Quote);
    }

    [Fact]
    public async Task Labeling_off_makes_no_model_call_for_tracker_records()
    {
        using var world = await Rig();

        var report = await world.Engine.Sync(source: null, MapWorld.HumanWith(Section(labeling: false)), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        Assert.Empty(world.Model.Requests);
        Assert.Empty((await world.Intents.Layer(Ct)).Value.Decisions);
    }
}
