using Aw.Kernel.Data;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>Issue #959: a needs-you row stored before 'open' was required can't be read back; cancelling its instance must not stall the projector.</summary>
[Lane(TestLane.Integration)]
public sealed class UnreadableNeedsYouRowTests : IDisposable
{
    private static readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));
    private static readonly string _address = _item.Value + "/step/1";

    private readonly TempHome _home = new();
    private readonly SliceHarness _slice = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 60_000)]
    public async Task A_transition_on_an_instance_whose_stored_row_lacks_open_projects_without_stalling_and_flags_the_page()
    {
        var ct = TestContext.Current.CancellationToken;
        using var views = new SqliteViews(_home.Path);
        var projections = Projections();
        _ = (await views.Store.Open(ViewSchema.Version, ct)).Value;
        Assert.Null((await views.Store.Apply(Parked(), projections, _slice.Clock.GetUtcNow(), ct)).Value.Stall);
        Assert.Contains("park:", await NeedsYouKeys(views, ct), StringComparison.Ordinal);   // the row the older build wrote
        await StripOpen(views, ct);

        var applied = (await views.Store.Apply([Cancelled()], projections, _slice.Clock.GetUtcNow(), ct)).Value;

        Assert.Null(applied.Stall);
        Assert.True(applied.Unreadable > 0);
        Assert.Equal(string.Empty, await NeedsYouKeys(views, ct));   // the cancel settled the instance: no park row is left
    }

    private static async Task StripOpen(SqliteViews views, CancellationToken ct)
    {
        var writable = (await views.Database.Writable(ct)).Value;
        await using var session = (await writable.BeginSession(ct)).Value;
        _ = await SqliteQueries.Execute(session, new SqlStatement("UPDATE view_needs_you SET row_json = json_remove(row_json, '$.row.open')"), ct);
        _ = await session.Commit(ct);
    }

    private static async Task<string> NeedsYouKeys(SqliteViews views, CancellationToken ct) =>
        string.Join(',', (await views.Reader.List<NeedsYouRowRecord>(ViewNames.NeedsYou, ct)).Value.Select(static r => r.Row.Id.Value));

    private IReadOnlyList<IProjection> Projections() =>
    [
        new RequestsProjection(_slice.Events, new PassThroughScrubber()),
        new ItemsProjection(_slice.Events, new PassThroughScrubber()),
        new ItemAliasesProjection(_slice.Events),
        new NeedsYouProjection(_slice.Events, new PassThroughScrubber(), []),
    ];

    private List<LedgerEvent> Parked()
    {
        var created = new ItemCreated(_slice.Ids.New<CommandId>(), _item, "the item", RepoName.From("agentic-workflow"), Link: null, Body: "the body", Owner: null);
        var park = new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);
        var produce = new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk);
        return
        [
            Recorded.Of(_slice.Events, EventTypes.ItemCreated, created, 1, _item),
            Recorded.Of(_slice.Events, EventTypes.WorkflowStarted, Row("produce", "start", produce, suspension: null), 2, _item),
            Recorded.Of(_slice.Events, EventTypes.WorkflowTransition, Row("produce", "step.completed", frame: null, park), 3, _item),
        ];
    }

    private LedgerEvent Cancelled() =>
        Recorded.Of(_slice.Events, EventTypes.WorkflowTransition,
            Row("done", "cancel", new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "cancelled"), 4, _item);

    private static WorkflowEventPayload Row(string to, string trigger, FrameSummary? frame, SuspensionSummary? suspension, string? outcome = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(_address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
