using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// The 5-minute index tick (<see cref="IIndexSync.Tick"/>) reads tracker records for decisions like the manual sync does (#1574).
/// </summary>
[Trait("Lane", "unit")]
public sealed class TickDecisionsTests
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

    private static ModelPurpose Extraction => ModelPurpose.DecisionExtraction;

    [Fact]
    public async Task Tick_turns_a_tracker_record_quoting_a_decision_into_a_decision_citing_it()
    {
        using var world = await Rig();

        var report = await world.Engine.Tick(MapWorld.HumanWith(Section(labeling: true)), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        Assert.Equal(_issue.Value, Assert.Single((await world.Records.List(new RecordFilter(), after: null, 10, Ct)).Value).Key.Value);   // the record itself did sync
        var decision = Assert.Single((await world.Intents.Layer(Ct)).Value.Decisions);
        Assert.Equal(_issue, decision.Record);
        Assert.Equal(Quote, decision.Quote);
    }

    [Fact]
    public async Task Tick_does_not_resend_an_unchanged_record_to_the_model()
    {
        using var world = await Rig();
        var scope = MapWorld.HumanWith(Section(labeling: true));

        _ = await world.Engine.Tick(scope, Ct);
        _ = await world.Engine.Tick(scope, Ct);

        _ = Assert.Single(world.Model.Requests, r => r.Purpose == Extraction);
    }
}
