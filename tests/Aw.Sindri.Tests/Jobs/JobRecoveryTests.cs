using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>Recovery at the next start (API P6 unit F, D8, Q6): a job the dead process left running ends failed SND-JOB-001, once, and is never re-run.</summary>
[Lane(TestLane.Unit)]
public sealed class JobRecoveryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recovery_ends_a_running_job_failed_SND_JOB_001_as_service_jobs()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        var job = await rig.Left(A.FirstJobId);
        rig.Caught();

        var ended = await rig.Recovery.Run(Token);

        Expect.Equal(1, ended.Value);
        var finished = Assert.Single(rig.Of(EventTypes.JobFinished));
        Expect.Equal(("service:jobs", job.Id, JobOutcome.Failed, SindriErrors.JobInterrupted.Code, "interrupted: the process stopped while it ran"),
            (finished.Actor, finished.Payload.Value.Job, finished.Payload.Value.Outcome, finished.Payload.Value.Code, finished.Payload.Value.Summary));
    }

    [Fact]
    public async Task Recovery_ends_a_waiting_job_failed_too_because_a_wait_does_not_survive_a_restart()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        var job = await rig.Left(A.FirstJobId, service: null, JobState.Waiting);
        rig.Caught();

        var ended = await rig.Recovery.Run(Token);

        Expect.Equal(1, ended.Value);
        var finished = Assert.Single(rig.Of(EventTypes.JobFinished));
        Expect.Equal((job.Id, JobOutcome.Failed, SindriErrors.JobInterrupted.Code), (finished.Payload.Value.Job, finished.Payload.Value.Outcome, finished.Payload.Value.Code));
    }

    [Fact]
    public async Task Recovery_is_idempotent()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        _ = await rig.Left(A.FirstJobId);
        rig.Caught();

        var first = await rig.Recovery.Run(Token);
        var second = await rig.Recovery.Run(Token);   // the view still reads 'running' (the projector hasn't caught up with the first end)

        _ = Assert.Single(rig.Of(EventTypes.JobFinished));
        Expect.Equal((1, 0), (first.Value, second.Value));   // the second run wrote nothing and says so
    }

    [Fact]
    public async Task An_interrupted_RunService_writes_no_service_ran()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        _ = await rig.Left(A.FirstJobId, ServiceName.From("heartbeat"));
        rig.Caught();

        _ = await rig.Recovery.Run(Token);

        Assert.DoesNotContain(rig.H.Store.Events, e => string.Equals(e.Envelope.Type, EventTypes.ServiceRan, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_job_this_process_runs_is_left_alone()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        var job = await rig.Left(A.FirstJobId);
        _ = rig.Registry.TryClaim(job.Kind, job.Key, job.Id, out _);
        rig.Caught();

        var ended = await rig.Recovery.Run(Token);

        Expect.Equal(0, ended.Value);
        Assert.Empty(rig.Of(EventTypes.JobFinished));
    }

    [Fact]
    public async Task A_job_that_started_after_the_first_attempt_is_left_alone()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        _ = await rig.Recovery.Run(Token);   // the head is taken here: the empty ledger
        _ = await rig.Left(A.FirstJobId);
        rig.Caught();

        var ended = await rig.Recovery.Run(Token);

        Expect.Equal(0, ended.Value);
    }

    [Fact]
    public async Task Views_behind_the_head_are_an_error_and_nothing_is_written()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        _ = await rig.Left(A.FirstJobId);
        rig.Progress.Publish(new ViewsStatus(Cursor: 0, LedgerHead: 1, ProjectedAt: null, Rebuilding: false, Stalled: true, StalledAt: 1, StalledType: "job.started"));

        var ended = await rig.Recovery.Run(Token);   // stalled before the head: no wait on the fake clock

        Expect.Equal(SindriErrors.ViewsBehind.Code, ended.Error.Code);
        Assert.Empty(rig.Of(EventTypes.JobFinished));
    }

    [Fact]
    public async Task Without_the_lease_it_waits()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        _ = await rig.Left(A.FirstJobId);
        rig.H.Held.Clear();
        rig.Caught();

        var ended = await rig.Recovery.Run(Token);

        Expect.Equal(SindriErrors.NotRunning.Code, ended.Error.Code);
        Assert.Empty(rig.Of(EventTypes.JobFinished));
    }
}
