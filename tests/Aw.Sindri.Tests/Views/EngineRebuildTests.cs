using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// 'sindri views rebuild' over the engine rows (API P11): the views a ledger gives folded one event at a time and the views a replay into
/// emptied tables gives are the same bytes, for the items and for the needs-you rows, through the SQLite store.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EngineRebuildTests : IDisposable
{
    private static readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));
    private static readonly string _first = _item.Value + "/step/1";
    private static readonly string _second = _item.Value + "/review/1";

    private readonly TempHome _home = new();
    private readonly SliceHarness _slice = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 60_000)]
    public async Task A_replay_into_emptied_views_gives_the_same_items_and_needs_you_rows_byte_for_byte()
    {
        var (incremental, rebuilt) = await Both(Ledger(firstEnds: false), TestContext.Current.CancellationToken);

        Assert.Contains("park:", incremental.NeedsYou, StringComparison.Ordinal);   // the scenario leaves the first root parked behind the ended second
        Assert.Contains("\"earlier\"", incremental.Items, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"pending\"", incremental.Decisions, StringComparison.Ordinal);   // the first root is still live, so the item hasn't ended by its workflow (Close would refuse too)
        Assert.DoesNotContain("voided", incremental.Decisions, StringComparison.Ordinal);
        Assert.Equal(incremental.Decisions, rebuilt.Decisions);
        Assert.Equal(incremental.Items, rebuilt.Items);
        Assert.Equal(incremental.NeedsYou, rebuilt.NeedsYou);
    }

    [Fact(Timeout = 60_000)]
    public async Task The_decision_is_voided_once_the_earlier_root_ends_too_incrementally_and_after_a_rebuild()
    {
        var (incremental, rebuilt) = await Both(Ledger(firstEnds: true), TestContext.Current.CancellationToken);

        Assert.Contains("\"state\":\"voided\"", incremental.Decisions, StringComparison.Ordinal);
        Assert.DoesNotContain("decision:", incremental.NeedsYou, StringComparison.Ordinal);
        Assert.DoesNotContain("park:", incremental.NeedsYou, StringComparison.Ordinal);   // the earlier root ended too, so its park is gone
        Assert.DoesNotContain("\"earlier\"", incremental.Items, StringComparison.Ordinal);
        Assert.Equal(incremental.Decisions, rebuilt.Decisions);
        Assert.Equal(incremental.Items, rebuilt.Items);
        Assert.Equal(incremental.NeedsYou, rebuilt.NeedsYou);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_replay_gives_the_same_rows_across_a_re_put_park_a_takeover_with_its_idle_reminder_a_detail_less_row_and_a_child_row()
    {
        var (incremental, rebuilt) = await Both(Lifecycle(), TestContext.Current.CancellationToken);

        Assert.Contains("takeover:", incremental.NeedsYou, StringComparison.Ordinal);   // the takeover is still idle when the ledger stops
        Assert.DoesNotContain("park:", incremental.NeedsYou, StringComparison.Ordinal);   // the park, re-put as an escalation, was replaced by the takeover
        Assert.Equal(incremental.Items, rebuilt.Items);
        Assert.Equal(incremental.NeedsYou, rebuilt.NeedsYou);
        Assert.Equal(incremental.Decisions, rebuilt.Decisions);
    }

    private async Task<((string Items, string NeedsYou, string Decisions) Incremental, (string Items, string NeedsYou, string Decisions) Rebuilt)> Both(List<LedgerEvent> ledger, CancellationToken ct)
    {
        using var views = new SqliteViews(_home.Path);
        var projections = Projections();
        _ = (await views.Store.Open(ViewSchema.Version, ct)).Value;
        foreach (var recorded in ledger)
        {
            Assert.Null((await views.Store.Apply([recorded], projections, _slice.Clock.GetUtcNow(), ct)).Value.Stall);
        }

        var incremental = await Snapshot(views, ct);
        _ = (await views.Store.Reset(ViewNames.All, ViewSchema.Version, _slice.Clock.GetUtcNow(), ct)).Value;
        Assert.Null((await views.Store.Apply(ledger, projections, _slice.Clock.GetUtcNow(), ct)).Value.Stall);
        _ = (await views.Store.FinishRebuild(ct)).Value;
        var rebuilt = await Snapshot(views, ct);

        return (incremental, rebuilt);
    }

    private static async Task<(string Items, string NeedsYou, string Decisions)> Snapshot(SqliteViews views, CancellationToken ct)
    {
        var items = await views.Reader.List<ItemRow>(ViewNames.Items, ct);
        var needs = await views.Reader.List<NeedsYouRowRecord>(ViewNames.NeedsYou, ct);
        var decisions = await views.Reader.List<HumanDecisionRow>(ViewNames.Decisions, ct);
        return (string.Join('\n', items.Value.Select(static r => JsonSerializer.Serialize(r, SliceHarness.Json))), string.Join('\n', needs.Value.Select(static r => JsonSerializer.Serialize(r, SliceHarness.Json))), string.Join('\n', decisions.Value.Select(static r => JsonSerializer.Serialize(r, SliceHarness.Json))));
    }

    private IReadOnlyList<IProjection> Projections() =>
    [
        new RequestsProjection(_slice.Events, new PassThroughScrubber()),   // before the items one, as registered
        new ItemsProjection(_slice.Events, new PassThroughScrubber()),
        new ItemAliasesProjection(_slice.Events),
        new NeedsYouProjection(_slice.Events, new PassThroughScrubber(), []),
    ];

    /// <summary>An item with two root instances: the first parks, the second (the latest) is answered and ends; with <paramref name="firstEnds"/> the first is then cancelled too.</summary>
    private List<LedgerEvent> Ledger(bool firstEnds)
    {
        var created = new ItemCreated(_slice.Ids.New<CommandId>(), _item, "the item", RepoName.From("agentic-workflow"), Link: null, Body: "the body", Owner: null);
        var filed = new RequestFiled(_slice.Ids.New<CommandId>(), A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, _item, RepoName.From("agentic-workflow"), "widen the scope");
        var park = new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);
        var produce = new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk);
        List<LedgerEvent> rows =
        [
            Recorded.Of(_slice.Events, EventTypes.ItemCreated, created, 1, _item),
            Recorded.Of(_slice.Events, EventTypes.RequestFiled, filed, 2, _item),
            Recorded.Of(_slice.Events, EventTypes.WorkflowStarted, Row(_first, "produce", "start", produce, suspension: null), 3, _item),
            Recorded.Of(_slice.Events, EventTypes.WorkflowStarted, Row(_second, "produce", "start", produce, suspension: null), 4, _item),
            Recorded.Of(_slice.Events, EventTypes.WorkflowTransition, Row(_first, "produce", "step.completed", frame: null, park), 5, _item),
            Recorded.Of(_slice.Events, EventTypes.WorkflowTransition, Row(_second, "done", "step.completed", new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "resolved"), 6, _item),
        ];
        if (firstEnds)
        {
            rows.Add(Recorded.Of(_slice.Events, EventTypes.WorkflowTransition, Row(_first, "done", "cancel", new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "cancelled"), 7, _item));
        }

        return rows;
    }

    /// <summary>One root instance's life with the clock moving a minute per row: desk task, park, the park re-put as an escalation, a child's row, a takeover, its idle reminder, a command row and a row stored without its detail.</summary>
    private List<LedgerEvent> Lifecycle()
    {
        var sequence = 0L;
        LedgerEvent Next<TPayload>(string type, TPayload payload)
        {
            _slice.Clock.Advance(TimeSpan.FromMinutes(1));
            return Recorded.Of(_slice.Events, type, payload, ++sequence, _item);
        }

        var created = new ItemCreated(_slice.Ids.New<CommandId>(), _item, "the item", RepoName.From("agentic-workflow"), Link: null, Body: "the body", Owner: null);
        var produce = new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk);
        var park = new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);
        var escalation = new SuspensionSummary(SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Gate, Mirrors: null);
        var takeover = new SuspensionSummary(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, Mirrors: null);
        var child = _first + "/child/1";
        return
        [
            Next(EventTypes.ItemCreated, created),
            Next(EventTypes.WorkflowStarted, Row(_first, "produce", "start", produce, suspension: null)),
            Next(EventTypes.WorkflowTransition, Row(_first, "produce", "step.completed", frame: null, park)),
            Next(EventTypes.WorkflowTransition, Row(_first, "produce", "child.escalated", frame: null, escalation)),
            Next(EventTypes.WorkflowStarted, Row(child, "review", "start", produce, suspension: null)),
            Next(EventTypes.WorkflowTransition, Row(_first, "produce", TriggerNames.Takeover, frame: null, takeover)),
            Next(EventTypes.WorkflowNotified, Row(_first, "produce", TriggerNames.TakeoverIdle, frame: null, takeover)),
            Next(EventTypes.WorkflowTransition, Row(_first, "produce", "command.note", frame: null, takeover)),
            Next(EventTypes.WorkflowTransition, new WorkflowEventPayload(_first, "produce", "produce", "command.note", Outcome: null, Run: null, Detail: null)),
        ];
    }

    private static WorkflowEventPayload Row(string address, string to, string trigger, FrameSummary? frame, SuspensionSummary? suspension, string? outcome = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
