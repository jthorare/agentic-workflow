using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.CancelWorkflowRun;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CancelWorkflowRun;

/// <summary>'sindri wf cancel': a run in flight is cancelled through the canceller; an unknown or ended run is refused.</summary>
[Lane(TestLane.Unit)]
public sealed class CancelWorkflowRunServiceTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly FakeGraphRunCanceller _canceller = new();
    private readonly SliceHarness _h = new();
    private readonly CancelWorkflowRunService _service;

    public CancelWorkflowRunServiceTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new CancelWorkflowRunService(_runs, _canceller, _h.Store, _h.Held, _h.Events);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(WorkflowRunStatus.Running)]
    [InlineData(WorkflowRunStatus.Waiting)]
    public async Task A_run_in_flight_is_cancelled_through_the_canceller(WorkflowRunStatus status)
    {
        var run = await Insert(1, status);

        var result = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(run.Id, result.Value.Run);
        Assert.Equal([run.Id], _canceller.Cancelled);
    }

    [Fact]
    public async Task Cancelling_a_queued_run_marks_it_cancelled_without_asking_temporal()
    {
        _ = await _h.HoldLease();
        var run = await Insert(4, WorkflowRunStatus.Queued);   // a queued run has no Temporal workflow yet

        var result = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(run.Id, result.Value.Run);
        Assert.Empty(_canceller.Cancelled);
        Assert.Equal(WorkflowRunStatus.Cancelled, (await _runs.Get(run.Id, Token)).Value.Status);
        var recorded = Assert.Single(_h.Store.Events);   // the same event, with the same key, the workflow's own end would write
        Assert.Equal(EventTypes.WorkflowRunCancelled, recorded.Envelope.Type);
        Assert.Equal($"workflow.run.cancelled:{run.Id.Value}", recorded.IdempotencyKey);
    }

    [Fact]
    public async Task Cancelling_a_queued_test_run_appends_workflow_run_cancelled_with_mode_test()
    {
        _ = await _h.HoldLease();
        var test = new WorkflowRun(WorkflowRunId.Of(_slug, 1, 9), _workflow, 1, """{"kind":"manual"}""", WorkflowRunStatus.Queued, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null, WorkflowRunMode.Test);
        _ = (await _runs.Insert(test, Token)).Value;

        _ = (await _service.Handle(new CancelWorkflowRunRequest(test.Id, "owner"), Token)).Value;

        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal(WorkflowRunMode.Test, _h.Events.Payload<WorkflowRunCancelledPayload>(recorded).Value.Mode);
    }

    [Fact]
    public async Task A_queued_cancel_whose_event_was_lost_is_finished_by_the_retry_once()
    {
        var run = await Insert(6, WorkflowRunStatus.Queued);
        _ = (await _runs.CancelQueued(run.Id, "cancelled while queued", Token)).Value;   // the store write happened, the lease went before the append
        _ = await _h.HoldLease();

        var retry = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);
        var again = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);

        Assert.True(retry.IsSuccess);
        Assert.True(again.IsSuccess);
        _ = Assert.Single(_h.Store.Events);   // the key is the run's, so two appends are one event
        Assert.Empty(_canceller.Cancelled);
    }

    [Fact]
    public async Task A_run_cancelled_for_another_reason_still_answers_ended()
    {
        _ = await _h.HoldLease();
        var run = await Insert(7, WorkflowRunStatus.Running);
        _ = (await _runs.SetStatus(run.Id, WorkflowRunStatus.Cancelled, outputs: null, error: "cancelled", Token)).Value;

        var result = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);

        Assert.Equal(SindriErrors.GraphRunEnded.Code, result.Error.Code);
        Assert.Empty(_h.Store.Events);
    }

    [Fact]
    public async Task Cancelling_a_queued_run_without_the_lease_is_refused_and_leaves_it_queued()
    {
        var run = await Insert(5, WorkflowRunStatus.Queued);

        var result = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);

        Assert.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
        Assert.Equal(WorkflowRunStatus.Queued, (await _runs.Get(run.Id, Token)).Value.Status);
        Assert.Empty(_h.Store.Events);
        Assert.Empty(_canceller.Cancelled);
    }

    [Fact]
    public async Task An_unknown_run_is_not_found()
    {
        var result = await _service.Handle(new CancelWorkflowRunRequest(WorkflowRunId.Of(_slug, 1, 7), "owner"), Token);

        Assert.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
        Assert.Empty(_canceller.Cancelled);
    }

    [Theory]
    [InlineData(WorkflowRunStatus.Succeeded)]
    [InlineData(WorkflowRunStatus.Failed)]
    [InlineData(WorkflowRunStatus.Cancelled)]
    public async Task A_run_that_already_ended_is_refused(WorkflowRunStatus status)
    {
        var run = await Insert(2, status);

        var result = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);

        Assert.Equal(SindriErrors.GraphRunEnded.Code, result.Error.Code);
        Assert.Empty(_canceller.Cancelled);
    }

    [Fact]
    public async Task A_cancel_the_canceller_cannot_deliver_is_returned_as_its_error()
    {
        var run = await Insert(3, WorkflowRunStatus.Running);
        _canceller.Fail = SindriErrors.NotRunning.With("temporal is down");

        var result = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);

        Assert.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }

    private async Task<WorkflowRun> Insert(int occurrence, WorkflowRunStatus status)
    {
        var run = new WorkflowRun(WorkflowRunId.Of(_slug, 1, occurrence), _workflow, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null);
        return (await _runs.Insert(run, Token)).Value;
    }
}
