using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'workflow-runs' view fold (issue 2258, PR 6): one row per graph run, from its six 'workflow.run.*' and 'workflow.node.*' events.</summary>
[Lane(TestLane.Unit)]
public sealed class RunsProjectionTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@3:12");
    private static readonly WorkflowRunId _other = WorkflowRunId.From("nightly@3:13");
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly NodeId _fetch = NodeId.From("fetch");
    private static readonly NodeId _notify = NodeId.From("notify");
    private readonly ViewsHarness _v = new();
    private long _sequence;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LedgerEvent Event<TPayload>(string type, TPayload payload) => Recorded.Of(_v.Slice.Events, type, payload, ++_sequence);

    private LedgerEvent Started(WorkflowRunId? run = null) => Event(EventTypes.WorkflowRunStarted, new WorkflowRunStartedPayload(run ?? _run, _workflow, 3, "{\"kind\":\"manual\"}", "human", WorkflowRunMode.Live));

    private async Task<WorkflowRunRow?> Row(WorkflowRunId? run = null) => (await _v.Views.Get<WorkflowRunRow>(ViewNames.WorkflowRuns, (run ?? _run).Value, Token)).Value;

    [Fact]
    public async Task A_started_run_is_a_running_row_with_its_workflow_revision_and_trigger()
    {
        var started = Started();
        await _v.Apply([started]);

        var row = (await Row())!;

        Expect.Equal((_run, _workflow, 3, "{\"kind\":\"manual\"}", "running"), (row.Id, row.WorkflowId, row.Revision, row.Trigger, row.Status));
        Expect.Equal((started.Envelope.OccurredAt, 1L), (row.StartedAt, row.Sequence));
        Assert.Null(row.EndedAt);
        Assert.Null(row.Error);
        Assert.Null(row.LastNodeId);
        Assert.Null(row.ItemId);
    }

    [Fact]
    public async Task The_row_names_the_item_the_started_event_was_recorded_for()
    {
        await _v.Fold(EventTypes.WorkflowRunStarted, new WorkflowRunStartedPayload(_run, _workflow, 3, "{}", "human", WorkflowRunMode.Live), A.FirstItemId);

        Assert.Equal(A.FirstItemId, (await Row())!.ItemId);
    }

    [Fact]
    public async Task A_run_that_finished_is_succeeded_and_ended_when_the_event_was_recorded()
    {
        var started = Started();
        var finished = Event(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(_run, "succeeded", Error: null, "service:workflow", WorkflowRunMode.Live));
        await _v.Apply([started, finished]);

        var row = (await Row())!;

        Expect.Equal(("succeeded", finished.Envelope.OccurredAt, 2L), (row.Status, row.EndedAt, row.Sequence));
        Assert.Null(row.Error);
    }

    [Fact]
    public async Task A_run_that_failed_is_failed_with_the_error_the_event_gave()
    {
        await _v.Apply([Started(), Event(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(_run, "failed", "node 'fetch' failed: 502", "service:workflow", WorkflowRunMode.Live))]);

        var row = (await Row())!;

        Expect.Equal(("failed", "node 'fetch' failed: 502"), (row.Status, row.Error));
        Assert.True(row.EndedAt is not null);
    }

    [Fact]
    public async Task A_cancelled_run_is_cancelled_with_its_reason_and_an_end_time()
    {
        var started = Started();
        var cancelled = Event(EventTypes.WorkflowRunCancelled, new WorkflowRunCancelledPayload(_run, "owner stopped it", "human", WorkflowRunMode.Live));
        await _v.Apply([started, cancelled]);

        var row = (await Row())!;

        Expect.Equal(("cancelled", "owner stopped it", cancelled.Envelope.OccurredAt), (row.Status, row.Error, row.EndedAt));
    }

    [Fact]
    public async Task Each_node_event_moves_the_last_node_and_leaves_the_status_alone()
    {
        await _v.Apply([Started()]);
        await _v.Apply([Event(EventTypes.WorkflowNodeStarted, new WorkflowNodeStartedPayload(_run, _fetch, 0, "service:workflow"))]);
        Assert.Equal(_fetch, (await Row())!.LastNodeId);

        await _v.Apply([Event(EventTypes.WorkflowNodeFinished, new WorkflowNodeFinishedPayload(_run, _fetch, 0, "succeeded", Truncated: false, "service:workflow"))]);
        await _v.Apply([Event(EventTypes.WorkflowNodeStarted, new WorkflowNodeStartedPayload(_run, _notify, 0, "service:workflow"))]);
        Assert.Equal(_notify, (await Row())!.LastNodeId);

        await _v.Apply([Event(EventTypes.WorkflowNodeFailed, new WorkflowNodeFailedPayload(_run, _notify, 0, "no channel", "service:workflow"))]);

        var row = (await Row())!;
        Expect.Equal((_notify, "running", 5L), (row.LastNodeId, row.Status, row.Sequence));
        Assert.Null(row.Error);   // a failed node is the run's to report: only workflow.run.finished sets the error
    }

    [Fact]
    public async Task Another_runs_events_leave_the_row_alone()
    {
        await _v.Apply([Started(), Started(_other), Event(EventTypes.WorkflowRunCancelled, new WorkflowRunCancelledPayload(_other, Reason: null, "human", WorkflowRunMode.Live))]);

        Expect.Equal(("running", "cancelled"), ((await Row())!.Status, (await Row(_other))!.Status));
    }

    [Fact]
    public async Task A_node_event_for_a_run_with_no_row_makes_none()
    {
        await _v.Apply([Event(EventTypes.WorkflowNodeStarted, new WorkflowNodeStartedPayload(_run, _fetch, 0, "service:workflow"))]);

        Assert.Null(await Row());
    }

    [Fact]
    public async Task Folding_the_same_events_again_gives_the_same_row()
    {
        LedgerEvent[] page =
        [
            Started(),
            Event(EventTypes.WorkflowNodeStarted, new WorkflowNodeStartedPayload(_run, _fetch, 0, "service:workflow")),
            Event(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(_run, "failed", "boom", "service:workflow", WorkflowRunMode.Live)),
        ];
        await _v.Apply(page);
        var first = await Row();

        await _v.Apply(page);

        Assert.Equal(first, await Row());
    }

    [Fact]
    public async Task A_replayed_started_event_does_not_reopen_a_finished_run()
    {
        var started = Started();
        await _v.Apply([started, Event(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(_run, "succeeded", Error: null, "service:workflow", WorkflowRunMode.Live))]);

        await _v.Apply([started]);

        Assert.Equal("succeeded", (await Row())!.Status);
    }
}
