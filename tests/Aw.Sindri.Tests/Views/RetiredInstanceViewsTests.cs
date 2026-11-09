using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// The recipe engine's 'workflow.*' events are history only (#2258, v2 PR 4b): an item whose root instance was parked or waiting at a desk when the
/// recipe engine went reads plain open, never "parked" or "needs an answer", and no needs-you row comes from those events, before and after the
/// 'workflow.retired' note, folded one event at a time and after a views rebuild (the schema bump replays the whole ledger).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RetiredInstanceViewsTests : IDisposable
{
    private static readonly ItemId _parkedItem = ItemId.From("ITM-" + new string('1', 32));
    private static readonly ItemId _deskItem = ItemId.From("ITM-" + new string('2', 32));
    private static readonly string _parked = _parkedItem.Value + "/step/1";
    private static readonly string _desk = _deskItem.Value + "/step/1";

    private readonly TempHome _home = new();
    private readonly SliceHarness _slice = new();

    public void Dispose() => _home.Dispose();

    [Theory(Timeout = 60_000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recipe_era_instance_events_leave_both_items_open_with_no_needs_you_row_incrementally_and_after_a_rebuild(bool retire)
    {
        var (incremental, rebuilt) = await Both(Ledger(retire), TestContext.Current.CancellationToken);

        foreach (var (items, needs) in new[] { incremental, rebuilt }.Select(static r => (r.Items, r.NeedsYou)))
        {
            Assert.Equal(2, items.Count);
            Assert.All(items.Values, static row => Assert.Equal(ItemState.New, row.State));
            Assert.Empty(needs);
        }

        Assert.Equal(incremental.Items.Values.Select(static r => (r.Id, r.State)), rebuilt.Items.Values.Select(static r => (r.Id, r.State)));
    }

    private async Task<(Folded Incremental, Folded Rebuilt)> Both(List<LedgerEvent> ledger, CancellationToken ct)
    {
        using var views = new SqliteViews(_home.Path);
        var projections = Projections();
        _ = (await views.Store.Open(ViewSchema.Version, ct)).Value;
        foreach (var recorded in ledger)
        {
            Assert.Null((await views.Store.Apply([recorded], projections, _slice.Clock.GetUtcNow(), ct)).Value.Stall);
        }

        var incremental = await Snapshot(views, ct);
        _ = (await views.Store.Reset(ViewNames.All, ViewSchema.Version, _slice.Clock.GetUtcNow(), 0, ct)).Value;
        Assert.Null((await views.Store.Apply(ledger, projections, _slice.Clock.GetUtcNow(), ct)).Value.Stall);
        _ = (await views.Store.FinishRebuild(ct)).Value;
        return (incremental, await Snapshot(views, ct));
    }

    private static async Task<Folded> Snapshot(SqliteViews views, CancellationToken ct)
    {
        var items = await views.Reader.List<ItemRow>(ViewNames.Items, ct);
        var needs = await views.Reader.List<NeedsYouRowRecord>(ViewNames.NeedsYou, ct);
        return new Folded(items.Value.ToDictionary(static r => r.Id.Value, StringComparer.Ordinal), [.. needs.Value]);
    }

    private IReadOnlyList<IProjection> Projections() =>
    [
        new RequestsProjection(_slice.Events, new PassThroughScrubber()),
        new ItemsProjection(_slice.Events, new PassThroughScrubber()),
        new ItemAliasesProjection(_slice.Events),
        new NeedsYouProjection(_slice.Events, new PassThroughScrubber(), []),
    ];

    /// <summary>Two items: one root instance parked, one waiting at a desk; with <paramref name="retire"/> each instance then gets the note the retire pass writes.</summary>
    private List<LedgerEvent> Ledger(bool retire)
    {
        var repo = RepoName.From("agentic-workflow");
        var park = new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);
        var produce = new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk);
        var sequence = 0L;
        LedgerEvent Next<TPayload>(string type, TPayload payload, ItemId item) => Recorded.Of(_slice.Events, type, payload, ++sequence, item);
        List<LedgerEvent> rows =
        [
            Next(EventTypes.ItemCreated, new ItemCreated(_slice.Ids.New<CommandId>(), _parkedItem, "parked", repo, Link: null, Body: "b", Owner: null), _parkedItem),
            Next(EventTypes.ItemCreated, new ItemCreated(_slice.Ids.New<CommandId>(), _deskItem, "desk", repo, Link: null, Body: "b", Owner: null), _deskItem),
            Next(EventTypes.WorkflowStarted, Row(_parked, "produce", "start", produce, suspension: null), _parkedItem),
            Next(EventTypes.WorkflowTransition, Row(_parked, "produce", "step.completed", frame: null, park), _parkedItem),
            Next(EventTypes.WorkflowStarted, Row(_desk, "produce", "start", produce, suspension: null), _deskItem),
        ];
        if (retire)
        {
            rows.Add(Next(EventTypes.WorkflowRetired, new WorkflowEventPayload(_parked, "", "retired", "retire", Outcome: "retired", Run: null), _parkedItem));
            rows.Add(Next(EventTypes.WorkflowRetired, new WorkflowEventPayload(_desk, "", "retired", "retire", Outcome: "retired", Run: null), _deskItem));
        }

        return rows;
    }

    private static WorkflowEventPayload Row(string address, string to, string trigger, FrameSummary? frame, SuspensionSummary? suspension)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address, "from", to, trigger, Outcome: null, Run: null, detail);
    }

    private sealed record Folded(Dictionary<string, ItemRow> Items, List<NeedsYouRowRecord> NeedsYou);
}
