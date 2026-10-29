using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.Setup;

/// <summary>A map build waiting to retry (setup-once spec §6, §8), and how the Onyx setup job reads back from the jobs view.</summary>
public sealed class SetupStepsDeferredAndRetryTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_map_build_waiting_to_retry_is_running_with_the_wait_and_the_reason_and_offers_the_retry()
    {
        var fact = new MapFact(MapPhase.Building, new SetupProgress(0, 1, "files")) { Retry = new MapRetry(TimeSpan.FromMinutes(5), "the code index was busy") };

        var step = SetupSteps.Map(fact);

        Assert.Equal((SetupState.Running, "Retrying in 5 minutes: the code index was busy.", "RefreshContextMap"), (step.State, step.Detail, step.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_retry_of_one_minute_is_said_in_the_singular()
    {
        var step = SetupSteps.Map(new MapFact(MapPhase.Building) { Retry = new MapRetry(TimeSpan.FromMinutes(1), "the code index was busy") });

        Assert.Equal("Retrying in 1 minute: the code index was busy.", step.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_setup_that_ran_out_of_time_or_was_cut_by_a_restart_is_todo_again_not_failed()
    {
        Assert.Equal(OnyxJobPhase.None, OnyxSetupJobFact.From(Job(Core.Jobs.JobState.Failed, "SND-SETUP-006")).Phase);
        Assert.Equal(OnyxJobPhase.None, OnyxSetupJobFact.From(Job(Core.Jobs.JobState.Failed, "SND-JOB-001")).Phase);
        Assert.Equal(OnyxJobPhase.None, OnyxSetupJobFact.From(Job(Core.Jobs.JobState.Cancelled, code: null)).Phase);
        Assert.Equal(OnyxJobPhase.Failed, OnyxSetupJobFact.From(Job(Core.Jobs.JobState.Failed, "SND-ONYX-005")).Phase);
        Assert.Equal(OnyxJobPhase.WaitingForLogin, OnyxSetupJobFact.From(Job(Core.Jobs.JobState.Waiting, code: null)).Phase);
        Assert.Equal(OnyxJobPhase.None, OnyxSetupJobFact.From(newest: null).Phase);
    }

    private static Core.Jobs.JobRow Job(Core.Jobs.JobState state, string? code) =>
        new(Core.Ids.JobId.From("JOB-" + new string('1', 32)), OnyxSetupJob.Kind, OnyxSetupJob.Key, Item: null, Repo: null, Service: null, state, "human:app", DateTimeOffset.UnixEpoch, EndedAt: null, [], Rounds: 0, "It stopped.", code, Result: null, CancelledBy: null, Sequence: 1);
}
