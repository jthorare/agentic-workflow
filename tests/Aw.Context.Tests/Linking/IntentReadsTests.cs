using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;
using Aw.Context.Tests.Map;
using static Aw.Context.Tests.SyncSources.ExtractDecisionsTests;

namespace Aw.Context.Tests.Linking;

/// <summary>
/// The intent layer through every graph read (S-5B): lenses and node reads see it, the deny cut and a session's scope apply to it, and the
/// map's own writes (export, status) stay free of it.
/// </summary>
[Trait("Lane", "integration")]
public sealed class IntentReadsTests
{
    private static readonly SourceId _linear = SourceId.From("linear");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lenses_show_the_synced_record_and_its_decision_in_the_intent_band()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope());

        var view = (await rig.World.Engine.Lenses(Toggle, read: MapRead.Current, depth: null, MapWorld.Human, Ct)).Value;

        Assert.Contains(view.Bands.Intent.Records, r => r.Key.Value == "record:" + R103);
        Assert.Contains(view.Bands.Intent.Decisions, d => d.Node.Value == DecisionRow.KeyFor(RecordKey.From(R103), Q103).Value);
    }

    [Fact]
    public async Task A_contradicts_verdict_shows_as_a_lens_conflict_and_an_agrees_one_does_not()
    {
        await using var contradicting = await Rig.Start("contradicts");
        await using var agreeing = await Rig.Start("agrees");
        _ = await contradicting.Sync(Scope());
        _ = await agreeing.Sync(Scope());

        var bad = (await contradicting.World.Engine.Lenses(Toggle, read: MapRead.Current, depth: null, MapWorld.Human, Ct)).Value;
        var fine = (await agreeing.World.Engine.Lenses(Toggle, read: MapRead.Current, depth: null, MapWorld.Human, Ct)).Value;

        var conflict = Assert.Single(bad.Conflicts, c => c.Kind == ConflictKind.Contradicts);
        Assert.Equal(Toggle, conflict.Node.Value);
        Assert.StartsWith("decision:", conflict.Other, StringComparison.Ordinal);
        Assert.DoesNotContain(fine.Conflicts, c => c.Kind == ConflictKind.Contradicts);
    }

    [Fact]
    public async Task Replacing_decision_shows_as_a_supersedes_lens_conflict()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope());

        var view = (await rig.World.Engine.Lenses(Resize, read: MapRead.Current, depth: null, MapWorld.Human, Ct)).Value;

        Assert.Contains(view.Conflicts, c => c.Kind == ConflictKind.Supersedes);
    }

    [Fact]
    public async Task The_deny_cut_applies_to_intent_layer_records_and_their_decisions()
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();
        var secret = RecordKey.From("repo:aw-split-panes/secrets/plan.md");
        var open = RecordKey.From("repo:aw-split-panes/docs/plan.md");
        var target = NodeKey.From(Orientation);
        _ = await world.Intents.CommitPage(new IntentPage(_linear,
            [new IntentRecord(secret, _linear, "v1", "Secret plan", ["SEC-1"]), new IntentRecord(open, _linear, "v1", "Open plan", ["OPN-1"])],
            [new IntentLink(secret, target, LinkRule.Identifier, 0.7, "aa"), new IntentLink(open, target, LinkRule.Identifier, 0.7, "bb")], Cursor: null, new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero)), Ct);

        var view = (await world.Engine.Lenses(Orientation, read: MapRead.Current, depth: null, MapWorld.Human, Ct)).Value;

        Assert.Contains(view.Bands.Intent.Records, r => r.Key.Value == "record:" + open.Value);
        Assert.DoesNotContain(view.Bands.Intent.Records, r => r.Key.Value == "record:" + secret.Value);   // a default deny glob (**/secrets/**) names it
    }

    [Fact]
    public async Task Export_and_status_stay_free_of_the_intent_layer()
    {
        await using var rig = await Rig.Start();
        var before = (await rig.World.Map.ExportMap(path: null, MapWorld.Human, Ct)).Value;
        var keysBefore = rig.World.Exports.Documents[before.Path].Nodes.Select(n => n.Key.Value).ToList();   // the fake store reuses the file name, so read them now
        _ = await rig.Sync(Scope());

        var after = (await rig.World.Map.ExportMap(path: null, MapWorld.Human, Ct)).Value;

        Assert.Equal(before.Counts, after.Counts);
        var document = rig.World.Exports.Documents[after.Path];
        Assert.Equal(keysBefore, [.. document.Nodes.Select(n => n.Key.Value)], StringComparer.Ordinal);   // the same nodes: no synced record or decision
        Assert.Equal(before.Counts, (await rig.World.Map.MapStatus(anchor: null, MapWorld.Human, Ct)).Value.Counts);
    }

    private const string Orientation = "feature:layout.split-pane.orientation";
}
