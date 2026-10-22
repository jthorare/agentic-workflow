using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.ConnectOnyx;
using Aw.Sindri.Features.SetupOnyx;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.SetupOnyx;

/// <summary>The Onyx setup job and ConnectOnyx on the real job runtime (setup-once spec §5.6): the stack comes up, the job parks without a slot, the owner's login wakes it, and every ending leaves the slot empty.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupOnyxJobTests
{
    private static string[] JobEvents(SetupOnyxRig rig) =>
        [.. rig.Runtime.Events.Select(static e => e.Envelope.Type).Where(static t => t.StartsWith("job.", StringComparison.Ordinal) && !string.Equals(t, EventTypes.JobProgress, StringComparison.Ordinal))];

    private static JobFinished Finished(SetupOnyxRig rig) =>
        rig.Runtime.H.Events.Payload<JobFinished>(Assert.Single(rig.Runtime.Events, static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal))).Value;

    [Fact]
    public async Task The_job_brings_Onyx_up_then_parks_on_the_login_signal_and_holds_no_slot()
    {
        await using var rig = new SetupOnyxRig();

        var job = await rig.Start();
        await rig.Waiting(job);

        Expect.Equal(0, rig.Runtime.Runner.Running);   // parked: no slot
        Expect.Equal(new Uri("http://127.0.0.1:3000/auth/signup"), Assert.Single(rig.Browser.Opened));
        Assert.Contains(rig.Progress.Lines, static l => l.Contains("http://127.0.0.1:3000/auth/signup", StringComparison.Ordinal));
        Expect.Sequence(["job.started", "job.waiting"], JobEvents(rig));
        _ = await rig.Runtime.Cancel(job);
        await rig.Runtime.Quiesce();
    }

    [Fact]
    public async Task A_second_start_while_one_waits_is_a_conflict()
    {
        await using var rig = new SetupOnyxRig();
        var job = await rig.Start();
        await rig.Waiting(job);

        var again = await rig.Runtime.Start(new SetupOnyxRequest(), SetupOnyxRig.Human());

        Assert.True(again.IsError);
        Expect.Equal(SindriErrors.JobConflict.Code, again.Error.Code);
        _ = await rig.Runtime.Cancel(job);
        await rig.Runtime.Quiesce();
    }

    [Fact]
    public async Task ConnectOnyx_wakes_the_same_job_which_connects_with_the_login_and_ends_succeeded()
    {
        await using var rig = new SetupOnyxRig();
        var job = await rig.Start();
        await rig.Waiting(job);

        var answer = await rig.Connect(job);
        await rig.Runtime.Quiesce();

        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.ToString() : null);
        Expect.Equal(job, answer.Value.JobId);
        Expect.Sequence(["job.started", "job.waiting", "job.resumed", "job.finished"], JobEvents(rig));
        var finished = Finished(rig);
        Expect.Equal(JobOutcome.Succeeded, finished.Outcome);
        Expect.Equal("keychain:onyx/sindri-onyx", finished.Result!.Value.GetProperty("keyPointer").GetString());
        Expect.Equal(("http://127.0.0.1:3000", "keychain:onyx/sindri-onyx", "v4.9.0"), rig.Profile.Section!.Value);
        Assert.Null(rig.Slots.Take(job));   // used and gone
        Assert.False(rig.Signals.IsWaiting(job, OnyxSetupJob.Signal));
        Expect.Equal(0, rig.Prompts.Asked.Count);   // nothing was asked at a terminal
    }

    [Fact]
    public async Task ConnectOnyx_while_nothing_waits_is_SND_SETUP_006_and_keeps_nothing()
    {
        await using var rig = new SetupOnyxRig();
        await rig.Runtime.Open();
        var nobody = JobId.From("JOB-" + new string('0', 32));

        var answer = await rig.Connect(nobody);

        Assert.True(answer.IsError);
        Expect.Equal(SindriErrors.SetupOnyxNotWaiting.Code, answer.Error.Code);
        Assert.Null(rig.Slots.Take(nobody));
    }

    [Fact]
    public async Task ConnectOnyx_without_a_login_in_hand_is_SND_SETUP_005()
    {
        await using var rig = new SetupOnyxRig();
        var job = await rig.Start();
        await rig.Waiting(job);

        var answer = await rig.Runtime.RunPrepared<ConnectOnyxRequest, JobStartedResponse>(new ConnectOnyxRequest(job), SetupOnyxRig.Human(), static _ => { });

        Assert.True(answer.IsError);
        Expect.Equal(SindriErrors.SetupOnyxCredentialsMissing.Code, answer.Error.Code);
        Assert.True(rig.Signals.IsWaiting(job, OnyxSetupJob.Signal));   // the job still waits
        _ = await rig.Runtime.Cancel(job);
        await rig.Runtime.Quiesce();
    }

    [Fact]
    public async Task A_session_may_not_connect_and_the_job_keeps_waiting()
    {
        await using var rig = new SetupOnyxRig();
        var job = await rig.Start();
        await rig.Waiting(job);

        var answer = await rig.Runtime.RunPrepared<ConnectOnyxRequest, JobStartedResponse>(new ConnectOnyxRequest(job), A.Caller().Session().Build(),
            static _ => { });

        Assert.True(answer.IsError);
        Expect.Equal(SindriErrors.AuthRefused.Code, answer.Error.Code);
        Assert.True(rig.Signals.IsWaiting(job, OnyxSetupJob.Signal));
        _ = await rig.Runtime.Cancel(job);
        await rig.Runtime.Quiesce();
    }

    [Fact]
    public async Task Thirty_minutes_without_a_login_fail_the_job_with_the_006_code()
    {
        await using var rig = new SetupOnyxRig();
        var job = await rig.Start();
        await rig.Waiting(job);

        rig.Runtime.H.Clock.Advance(TimeSpan.FromMinutes(30));
        await rig.Runtime.Quiesce();

        var finished = Finished(rig);
        Expect.Equal((JobOutcome.Failed, SindriErrors.SetupOnyxNotWaiting.Code), (finished.Outcome, finished.Code));
        Assert.False(rig.Signals.IsWaiting(job, OnyxSetupJob.Signal));
        Assert.Null(rig.Slots.Take(job));
    }

    [Fact]
    public async Task Cancelling_a_waiting_job_ends_it_cancelled_and_wakes_nothing()
    {
        await using var rig = new SetupOnyxRig();
        var job = await rig.Start();
        await rig.Waiting(job);

        _ = await rig.Runtime.Cancel(job);
        await rig.Runtime.Quiesce();

        Expect.Sequence(["job.started", "job.waiting", "job.cancelled"], JobEvents(rig));
        Assert.False(rig.Signals.IsWaiting(job, OnyxSetupJob.Signal));
        Assert.Null(rig.Slots.Take(job));
    }

    [Fact]
    public async Task A_login_Onyx_refuses_ends_the_job_failed_with_Onyxs_code_and_empties_the_slot()
    {
        await using var rig = new SetupOnyxRig();
        rig.Provisioning.SignInError = SindriErrors.OnyxKeyFailed.With("Onyx refused that login (HTTP 400)");
        var job = await rig.Start();
        await rig.Waiting(job);

        _ = await rig.Connect(job);
        await rig.Runtime.Quiesce();

        var finished = Finished(rig);
        Expect.Equal((JobOutcome.Failed, SindriErrors.OnyxKeyFailed.Code), (finished.Outcome, finished.Code));
        Assert.Null(rig.Slots.Take(job));
        Assert.DoesNotContain(SetupOnyxRig.Sentinel, rig.Everything(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_login_that_is_not_an_admin_ends_the_job_failed_with_SND_ONYX_007()
    {
        await using var rig = new SetupOnyxRig();
        rig.Provisioning.AdminError = SindriErrors.OnyxNotAdmin.With("that login isn't an Onyx admin");
        var job = await rig.Start();
        await rig.Waiting(job);

        _ = await rig.Connect(job);
        await rig.Runtime.Quiesce();

        Expect.Equal(SindriErrors.OnyxNotAdmin.Code, Finished(rig).Code);
        Assert.Null(rig.Slots.Take(job));
    }

    [Fact]
    public async Task When_the_stack_will_not_come_up_the_job_fails_without_ever_waiting()
    {
        await using var rig = new SetupOnyxRig();
        rig.Up.Fails = SindriErrors.OnyxDockerUnavailable.With("docker isn't running");
        _ = await rig.Start();

        await rig.Runtime.Quiesce();

        Expect.Sequence(["job.started", "job.finished"], JobEvents(rig));
        Expect.Equal(SindriErrors.OnyxDockerUnavailable.Code, Finished(rig).Code);
    }

    [Fact]
    public async Task The_sentinel_login_reaches_no_ledger_event_log_line_progress_state_or_result_on_the_success_path()
    {
        await using var rig = new SetupOnyxRig();
        var job = await rig.Start();
        await rig.Waiting(job);

        _ = await rig.Connect(job);
        await rig.Runtime.Quiesce();

        Assert.DoesNotContain(SetupOnyxRig.Sentinel, rig.Everything(), StringComparison.Ordinal);
        Assert.DoesNotContain(SetupOnyxRig.OwnerEmail, rig.Everything(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sentinel_is_nowhere_after_a_refused_connect_or_a_timeout()
    {
        await using var rig = new SetupOnyxRig();
        var job = await rig.Start();
        await rig.Waiting(job);
        _ = await rig.Runtime.RunPrepared<ConnectOnyxRequest, JobStartedResponse>(new ConnectOnyxRequest(JobId.From("JOB-" + new string('0', 32))), SetupOnyxRig.Human(),
            services => services.GetRequiredService<IOnyxLoginHand>().Give(new OnyxLogin(SetupOnyxRig.OwnerEmail, new OnyxSecret(SetupOnyxRig.Sentinel))));

        rig.Runtime.H.Clock.Advance(TimeSpan.FromMinutes(30));
        await rig.Runtime.Quiesce();

        Assert.DoesNotContain(SetupOnyxRig.Sentinel, rig.Everything(), StringComparison.Ordinal);
    }
}
