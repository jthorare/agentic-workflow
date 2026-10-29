using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// Decision extraction, supersedes and intent-vs-intent conflicts inside a sync (S-5B, spec §6.1, §8.8, §8.9): the model behind
/// 'context.labeling', under 'maxTokensPerSync', over a Linear record feed and a scripted model.
/// </summary>
[Trait("Lane", "integration")]
public sealed class ExtractDecisionsTests
{
    internal const string R103 = "linear:issue/0e5a1b00-0000-4000-8000-000000000103";
    internal const string R104 = "linear:issue/0e5a1b00-0000-4000-8000-000000000104";
    internal const string R105 = "linear:issue/0e5a1b00-0000-4000-8000-000000000105";
    internal const string Toggle = "surface:command/toggle-orientation";
    internal const string Resize = "feature:layout.pane-resize";
    internal const string Q103 = "toggling orientation keeps the focused pane first";
    internal const string Q104 = "toggle-orientation always resets focus to the first pane";
    internal const string QOld = "the minimum pane width is 10 columns";
    internal const string QNew = "the minimum pane width is 20 columns";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static ContextScope Scope(bool labeling = true, int? perSync = null)
    {
        var cap = perSync is { } n ? ",\"maxTokensPerSync\":" + n.ToString(CultureInfo.InvariantCulture) : string.Empty;
        var section = labeling ? ",\"labeling\":{\"enabled\":true" + cap + "}" : ",\"labeling\":{\"enabled\":false}";
        return MapWorld.HumanWith("{\"sources\":{\"linear\":{\"teams\":[\"SPL\"]}}" + section + "}");
    }

    internal static string Plant(string text) => text
        .Replace("Implemented as setOrientation in SplitPane.ts", "Implemented as setOrientation in SplitPane.ts (layout.split-pane.orientation)", StringComparison.Ordinal)
        .Replace("Resize step defaults to 2 columns", "Resize step (layout.pane-resize) defaults to 2 columns", StringComparison.Ordinal)
        .Replace("Restoring a tab brings back orientation and focus", "Restoring a tab brings back orientation and focus; " + Q104, StringComparison.Ordinal);

    private static string Decisions(string quote, string node, string wording, string? replaces = null) =>
        $$"""{"quote":"{{quote}}","wording":"{{wording}}","decides":["{{node}}"]{{(replaces is null ? string.Empty : $",\"replaces\":\"{replaces}\"")}}}""";

    private static string Route(ModelRequest request, string verdict) => request.Purpose switch
    {
        ModelPurpose.DecisionExtraction when request.Prompt.Contains(R103, StringComparison.Ordinal) => "{\"decisions\":[" + Decisions(Q103, Toggle, "Toggling keeps focus first.") + "]}",
        ModelPurpose.DecisionExtraction when request.Prompt.Contains(R104, StringComparison.Ordinal) => "{\"decisions\":[" + Decisions(Q104, Toggle, "Toggling resets focus.") + "]}",
        ModelPurpose.DecisionExtraction when request.Prompt.Contains(R105, StringComparison.Ordinal) =>
            "{\"decisions\":[" + Decisions(QOld, Resize, "Ten columns.") + "," + Decisions(QNew, Resize, "Twenty columns.", QOld) + "]}",
        ModelPurpose.Adjudication => "{\"verdict\":\"" + verdict + "\"}",
        ModelPurpose.DecisionExtraction or ModelPurpose.Labeling or ModelPurpose.Rerank or ModelPurpose.Cleanup or ModelPurpose.Concepts or ModelPurpose.ConceptSynthesis => "{\"decisions\":[]}",
        _ => "{\"decisions\":[]}",
    };

    private static RecordItem Item(RecordWrite write)
    {
        var r = write.Record;
        return new RecordItem(r.Key, r.Kind, r.Title, Plant(string.Join('\n', write.Chunks.OrderBy(c => c.Seq).Select(c => c.Text))), Author: null, Url: null, r.State, r.StateRaw,
            r.CreatedAt, r.UpdatedAt, r.ClosedAt, r.Version, Links: [], Repo: null, AudienceKey.From("linear:SPL"));   // the audience the profile's team gives its issues
    }

    internal sealed class Rig : IAsyncDisposable
    {
        private Rig(MapWorld world)
        {
            World = world;
        }

        public MapWorld World { get; }

        public static async Task<Rig> Start(string verdict = "contradicts", Func<RecordItem, RecordItem>? shape = null, FakeLexicalLeg? lexical = null, IContextSource? intent = null)
        {
            var feed = new FakeRecordFeed(RecordSamples.Linear, items: [.. LinearIssueSeed.From(issues: null).Select(Item).Select(i => shape?.Invoke(i) ?? i)]);
            var world = new MapWorld(intent: intent, lexical: lexical, feeds: new FakeRecordFeedCatalog(feed));
            world.Model.Responder = r => Route(r, verdict);
            _ = await world.ImportFixture();
            return new Rig(world);
        }

        public async Task<SourceSync> Sync(ContextScope scope) => (await World.Engine.Sync("linear", scope, Ct)).Value.Sources[0];

        public async Task<IntentLayer> Layer() => (await World.Intents.Layer(Ct)).Value;

        public ValueTask DisposeAsync()
        {
            World.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Extract_stores_model_decisions_with_their_quote_and_a_decides_edge_to_a_linked_node()
    {
        await using var rig = await Rig.Start();

        var row = await rig.Sync(Scope());

        var layer = await rig.Layer();
        var decision = Assert.Single(layer.Decisions, d => d.Quote == Q103);
        Assert.Equal((R103, Provenance.Model), (decision.Record.Value, decision.Provenance));
        Assert.Contains(layer.DecisionEdges, e => e.Type == EdgeType.Decides && e.From.Value == decision.ToKey().Value && e.To.Value == Toggle);
        Assert.Equal(layer.Decisions.Count, row.Decisions);
        Assert.True(row.Tokens > 0);
        Assert.Equal(0, row.Queued);
    }

    [Fact]
    public async Task Extract_stops_at_maxTokensPerSync_and_leaves_the_rest_queued()
    {
        await using var all = await Rig.Start();
        _ = await all.Sync(Scope());
        var linked = all.World.Model.Requests.Count(r => r.Purpose == ModelPurpose.DecisionExtraction);
        await using var capped = await Rig.Start();

        var row = await capped.Sync(Scope(perSync: 1_100));

        var asked = capped.World.Model.Requests.Count(r => r.Purpose == ModelPurpose.DecisionExtraction);
        Assert.InRange(asked, 1, linked - 1);
        Assert.True(row.Tokens <= 1_100);
        Assert.Equal(linked - asked, row.Queued);
        Assert.Equal(ContextErrors.LabelingUnavailable.Code, row.Code);
    }

    [Fact]
    public async Task Extract_with_labeling_off_queues_and_reports_042()
    {
        await using var rig = await Rig.Start();

        var row = await rig.Sync(Scope(labeling: false));

        Assert.Empty(rig.World.Model.Requests);
        Assert.Equal(ContextErrors.LabelingUnavailable.Code, row.Code);
        var layer = await rig.Layer();
        Assert.Empty(layer.Decisions);
        Assert.Equal(layer.Records.Count(r => layer.Links.Any(l => l.Record.Value == r.Key.Value && l.Target.Kind != NodeKind.Record)), row.Queued);   // linked records wait; records no rule linked have nothing to extract
        Assert.True(row.Queued > 0);
    }

    [Fact]
    public async Task Queued_records_are_extracted_by_a_later_sync_once_labeling_is_on()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope(labeling: false));

        var row = await rig.Sync(Scope());

        Assert.Equal((0, null), (row.Queued, row.Code));
        Assert.Contains((await rig.Layer()).Decisions, d => d.Quote == Q103);
    }

    [Fact]
    public async Task Replacing_decision_in_same_record_writes_supersedes()
    {
        await using var rig = await Rig.Start();

        _ = await rig.Sync(Scope());

        var edges = (await rig.Layer()).DecisionEdges;
        var supersedes = Assert.Single(edges, e => e.Type == EdgeType.Supersedes);
        Assert.Equal((DecisionRow.KeyFor(RecordKey.From(R105), QNew).Value, DecisionRow.KeyFor(RecordKey.From(R105), QOld).Value), (supersedes.From.Value, supersedes.To.Value));
    }

    [Fact]
    public async Task Two_current_decisions_on_one_node_that_disagree_are_a_contradicts_conflict()
    {
        await using var rig = await Rig.Start("contradicts");

        _ = await rig.Sync(Scope());

        var conflict = Assert.Single((await rig.Layer()).Conflicts);
        Assert.Equal(ConflictVerdict.Contradicts, conflict.Verdict);
        Assert.Equal(new[] { DecisionRow.KeyFor(RecordKey.From(R103), Q103).Value, DecisionRow.KeyFor(RecordKey.From(R104), Q104).Value }.Order(StringComparer.Ordinal),
            [conflict.A.Value, conflict.B.Value], StringComparer.Ordinal);
    }

    [Fact]
    public async Task Agreeing_decisions_are_stored_as_agrees_and_Conflict_check_is_asked_once_per_pair()
    {
        await using var rig = await Rig.Start("agrees");

        _ = await rig.Sync(Scope());
        _ = await rig.Sync(Scope());

        Assert.Equal(ConflictVerdict.Agrees, Assert.Single((await rig.Layer()).Conflicts).Verdict);
        _ = Assert.Single(rig.World.Model.Requests, r => r.Purpose == ModelPurpose.Adjudication);
    }

    [Fact]
    public async Task A_malformed_model_answer_is_counted_refused_and_keeps_no_decision()
    {
        await using var rig = await Rig.Start();
        rig.World.Model.Responder = _ => "I would rather not.";

        var row = await rig.Sync(Scope());

        Assert.Empty((await rig.Layer()).Decisions);
        Assert.True(row.Refused > 0);
        Assert.Equal(0, row.Queued);   // the version was asked about, so it isn't asked again
    }

    [Fact]
    public async Task A_conflict_check_that_stops_leaves_the_record_queued_and_the_next_sync_checks_the_pair()
    {
        await using var rig = await Rig.Start("contradicts");
        rig.World.Model.Fail = r => r.Purpose == ModelPurpose.Adjudication ? ContextErrors.LabelingUnavailable.With("the model is down") : null;

        var first = await rig.Sync(Scope());

        Assert.Empty((await rig.Layer()).Conflicts);
        Assert.True(first.Queued > 0);   // the record whose pair wasn't checked is not marked done
        rig.World.Model.Fail = null;

        var second = await rig.Sync(Scope());

        Assert.Equal(ConflictVerdict.Contradicts, Assert.Single((await rig.Layer()).Conflicts).Verdict);
        Assert.Equal((0, null), (second.Queued, second.Code));
    }

    [Fact]
    public async Task A_time_cap_passing_during_a_model_call_lets_the_record_in_hand_finish_and_stops_before_the_next_record()
    {
        await using var whole = await Rig.Start();
        var all = (await whole.Sync(Scope())).Decisions;
        await using var rig = await Rig.Start();
        rig.World.Model.Fail = _ =>
        {
            rig.World.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));   // the first model call takes longer than the whole run may
            return null;
        };

        var capped = await rig.Sync(Scope());

        Assert.Equal(ContextErrors.SyncCapped.Code, capped.Code);
        _ = Assert.Single(rig.World.Model.Requests, r => r.Purpose == ModelPurpose.DecisionExtraction);
        Assert.Equal(0, capped.Queued);   // the record in hand was extracted; the others were not read yet, so none waits queued
        rig.World.Model.Fail = null;

        var resumed = await rig.Sync(Scope());

        Assert.Equal((0, null, all), (resumed.Queued, resumed.Code, resumed.Decisions));
    }

    [Fact]
    public async Task A_verdict_that_cannot_be_read_keeps_the_pair_pending_and_the_third_try_marks_it_refused()
    {
        await using var rig = await Rig.Start();
        var routed = rig.World.Model.Responder!;
        rig.World.Model.Responder = r => r.Purpose == ModelPurpose.Adjudication ? "no idea" : routed(r);
        int Asked() => rig.World.Model.Requests.Count(r => r.Purpose == ModelPurpose.Adjudication);

        var first = await rig.Sync(Scope());
        var afterFirst = (Asked(), first.Queued > 0, first.Refused > 0);
        var second = await rig.Sync(Scope());
        var afterSecond = (Asked(), second.Queued > 0);
        var third = await rig.Sync(Scope());
        var afterThird = (Asked(), third.Queued, (await rig.Layer()).Conflicts.Select(c => c.Verdict).ToList());
        var fourth = await rig.Sync(Scope());

        Assert.Equal((1, true, true), afterFirst);   // not answered: the record waits
        Assert.Equal((2, true), afterSecond);
        Assert.Equal(3, afterThird.Item1);
        Assert.Equal(0, afterThird.Queued);   // the third try gave up
        Assert.Equal([ConflictVerdict.Refused], afterThird.Item3);
        Assert.Equal((3, 0), (Asked(), fourth.Queued));   // and the pair is not asked again
    }

    [Fact]
    public async Task A_pair_whose_answer_was_unreadable_once_is_decided_by_the_next_readable_answer_and_leaves_the_queue()
    {
        await using var rig = await Rig.Start("contradicts");
        var routed = rig.World.Model.Responder!;
        var unreadable = true;
        rig.World.Model.Responder = r => r.Purpose == ModelPurpose.Adjudication && unreadable ? "no idea" : routed(r);

        var first = await rig.Sync(Scope());
        var pending = (await rig.Layer()).Conflicts.Select(c => (c.Verdict, c.Tries)).ToList();
        unreadable = false;
        var second = await rig.Sync(Scope());

        Assert.True(first.Queued > 0);
        Assert.Equal([(ConflictVerdict.Unread, 1)], pending);
        var decided = Assert.Single((await rig.Layer()).Conflicts);
        Assert.Equal((ConflictVerdict.Contradicts, 0), (decided.Verdict, decided.Tries));   // the readable answer replaced the unread row and reset the tries
        Assert.Equal((0, null), (second.Queued, second.Code));
        Assert.Equal(2, rig.World.Model.Requests.Count(r => r.Purpose == ModelPurpose.Adjudication));
    }
}
