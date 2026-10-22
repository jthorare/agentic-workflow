using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.Setup;

/// <summary>The two states the spec's goldens add to the derived steps (setup-once spec §6, §8): a map build waiting to retry, and Onyx deferred by the owner.</summary>
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
    public void Onyx_the_owner_chose_not_to_set_up_is_skipped_and_keeps_its_action()
    {
        var step = SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null) { Deferred = true });

        Assert.Equal((SetupState.Skipped, "You chose to set up Onyx later.", "SetupOnyx"), (step.State, step.Why, step.Action));
        Assert.Equal("Onyx isn't set up. Use Set up Onyx in the card's menu when you want search.", step.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Onyx_that_runs_is_done_whatever_the_owner_chose_before()
    {
        var step = SetupSteps.Onyx(new OnyxFact(OnyxState.Running, Unreachable: false, BlockedByCode: null) { Deferred = true });

        Assert.Equal(SetupState.Done, step.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_docker_is_said_before_the_owners_choice_and_has_no_action()
    {
        var step = SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: "SND-ONYX-001") { Deferred = true });

        Assert.Equal((SetupState.Skipped, "Docker isn't installed.", null), (step.State, step.Why, step.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void Onyx_whose_setup_waits_for_the_login_needs_you_with_the_sign_up_address_even_while_the_stack_runs()
    {
        var step = SetupSteps.Onyx(new OnyxFact(OnyxState.Running, Unreachable: false, BlockedByCode: null) { Job = OnyxJobPhase.WaitingForLogin, Port = 3001 });

        Assert.Equal((SetupState.NeedsYou, "ConnectOnyx", "Create your Onyx account in the browser, then enter your login here."), (step.State, step.Action, step.Why));
        Assert.Equal("Sign up at http://127.0.0.1:3001/auth/signup", step.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Onyx_whose_setup_job_works_is_running_and_says_its_last_line()
    {
        var step = SetupSteps.Onyx(new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null) { Job = OnyxJobPhase.Working, JobLine = "pull: 3 of 9 images" });

        Assert.Equal((SetupState.Running, "pull: 3 of 9 images", null), (step.State, step.Detail, step.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void Onyx_whose_setup_job_failed_is_failed_with_its_code_and_offers_the_setup_again()
    {
        var step = SetupSteps.Onyx(new OnyxFact(OnyxState.Running, Unreachable: false, BlockedByCode: null) { Job = OnyxJobPhase.Failed, JobFailure = ("SND-ONYX-008", "Onyx refused that login.") });

        Assert.Equal((SetupState.Failed, "SetupOnyx", "SND-ONYX-008", "Onyx refused that login."), (step.State, step.Action, step.Error!.Code, step.Why));
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
