using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.MarkWorkflowRunSeen;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.MarkWorkflowRunSeen;

/// <summary>E1 (Home spec 2026-11-12 §3.2): the owner opening a failed live run marks it seen, so its Home row clears. A session can't; a run that did not fail live is left alone; an unknown run is not found.</summary>
[Lane(TestLane.Unit)]
public sealed class MarkWorkflowRunSeenServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("nightly");

    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly InMemoryWorkflowRunStore _runs;

    public MarkWorkflowRunSeenServiceTests() => _runs = new InMemoryWorkflowRunStore(_clock);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private MarkWorkflowRunSeenService As(CallerContext caller) => new(_runs, new FixedCallerContextAccessor(caller), _clock);

    private async Task<WorkflowRunId> Insert(int occurrence, WorkflowRunStatus status, string mode = WorkflowRunMode.Live)
    {
        var id = WorkflowRunId.Of(_slug, 1, occurrence);
        _ = (await _runs.Insert(new WorkflowRun(id, _id, 1, """{"kind":"manual"}""", status, Outputs: null, Error: "boom", ItemId: null, KnownInstants.Epoch, KnownInstants.Epoch.AddMinutes(1), mode), Token)).Value;
        return id;
    }

    [Fact]
    public async Task The_owner_marks_a_failed_run_seen_at_the_clock_time()
    {
        var run = await Insert(1, WorkflowRunStatus.Failed);
        _clock.Advance(TimeSpan.FromHours(3));

        var result = await As(A.Caller().Human().Build()).Handle(new MarkWorkflowRunSeenRequest(run), Token);

        Assert.True(result.Value.Marked);
        Assert.Equal(_clock.GetUtcNow(), (await _runs.Get(run, Token)).Value.SeenAt);
    }

    [Fact]
    public async Task A_session_is_refused_and_the_run_stays_unseen()
    {
        var run = await Insert(1, WorkflowRunStatus.Failed);

        var result = await As(A.Caller().Session().Build()).Handle(new MarkWorkflowRunSeenRequest(run), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Null((await _runs.Get(run, Token)).Value.SeenAt);
    }

    [Theory]
    [InlineData(WorkflowRunStatus.Succeeded, WorkflowRunMode.Live, 2)]
    [InlineData(WorkflowRunStatus.Failed, WorkflowRunMode.Test, 500)]
    public async Task A_run_that_did_not_fail_live_is_left_alone_and_answers_ok(WorkflowRunStatus status, string mode, int occurrence)
    {
        var run = await Insert(occurrence, status, mode);

        var result = await As(A.Caller().Human().Build()).Handle(new MarkWorkflowRunSeenRequest(run), Token);

        Assert.False(result.Value.Marked);
        Assert.Null((await _runs.Get(run, Token)).Value.SeenAt);
    }

    [Fact]
    public async Task An_unknown_run_is_not_found()
    {
        var result = await As(A.Caller().Human().Build()).Handle(new MarkWorkflowRunSeenRequest(WorkflowRunId.Of(_slug, 1, 9)), Token);

        Assert.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
    }
}
