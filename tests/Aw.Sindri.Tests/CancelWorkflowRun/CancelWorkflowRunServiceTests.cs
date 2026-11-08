using Aw.Kernel.Testing;
using Aw.Sindri.Core;
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
    private readonly CancelWorkflowRunService _service;

    public CancelWorkflowRunServiceTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new CancelWorkflowRunService(_runs, _canceller);
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
        var run = await Insert(4, WorkflowRunStatus.Queued);   // a queued run has no Temporal workflow yet

        var result = await _service.Handle(new CancelWorkflowRunRequest(run.Id, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(run.Id, result.Value.Run);
        Assert.Empty(_canceller.Cancelled);
        Assert.Equal(WorkflowRunStatus.Cancelled, (await _runs.Get(run.Id, Token)).Value.Status);
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
