using System.Text.Json;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
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
        var (incremental, rebuilt) = await Both(Ledger(), TestContext.Current.CancellationToken);

        Assert.Contains("\"earlier\"", incremental.Items, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"pending\"", incremental.Decisions, StringComparison.Ordinal);   // the first root is still live, so the item hasn't ended by its workflow (Close would refuse too)
        Assert.DoesNotContain("voided", incremental.Decisions, StringComparison.Ordinal);
        Assert.Equal(incremental.Decisions, rebuilt.Decisions);
        Assert.Equal(incremental.Items, rebuilt.Items);
        Assert.Equal(incremental.NeedsYou, rebuilt.NeedsYou);
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
        _ = (await views.Store.Reset(ViewNames.All, ViewSchema.Version, _slice.Clock.GetUtcNow(), 0, ct)).Value;
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

    /// <summary>An item with two root instances: the first parks, the second (the latest) is answered and ends;</summary>
    private List<LedgerEvent> Ledger()
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
        return rows;
    }

    private static WorkflowEventPayload Row(string address, string to, string trigger, FrameSummary? frame, SuspensionSummary? suspension, string? outcome = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
