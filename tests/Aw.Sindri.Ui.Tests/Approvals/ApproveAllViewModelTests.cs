using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.Tests.Approvals;

/// <summary>The approve-all dialog (setup-once spec §4, §5.3): the plan in words, the challenge for its digest, one hold or typed answer, the job followed.</summary>
[Lane(TestLane.Unit)]
public sealed class ApproveAllViewModelTests
{
    private readonly ApproveAllFixture _f = new();

    private static CancellationToken Token => ApproveAllFixture.Token;

    [Fact]
    public async Task Open_reads_the_plan_and_asks_for_the_challenge_of_its_digest()
    {
        var dialog = await _f.Opened();

        Assert.Equal((ApproveAllPhase.Plan, true, "2 workflows will be approved"), (dialog.Phase, dialog.IsOpen, dialog.Heading));
        Assert.Equal("approve-all 1c1fd4", Assert.Single(_f.Server.Challenges).Action);
        Assert.Equal((true, true, "approve-all 1c1fd4"), (dialog.CanApprove, dialog.OffersHold, dialog.TypedAlternative));
    }

    [Fact]
    public async Task Each_workflow_shows_its_name_and_why_it_is_there_and_keeps_its_id_for_details()
    {
        var dialog = await _f.Opened();

        var step = dialog.Rows[0];
        Assert.Equal(("One step", "Updated since you last approved it"), (step.Name, step.Reason));
        Assert.Equal(["Starts an AI agent session in a repo you added", "Puts a question or notice in your Needs you list"], step.Capabilities);
        Assert.Equal(("Work item", "New: you haven't approved it before"), (dialog.Rows[1].Name, dialog.Rows[1].Reason));
        Assert.DoesNotContain("workflow.", step.AccessibleName, StringComparison.Ordinal);
        Assert.Contains(dialog.DetailLines, l => string.Equals(l, "workflow.step · v2 · 1a2b3c4d5e6f", StringComparison.Ordinal));
        Assert.Equal("plan 1c1fd4", dialog.DetailLines[0]);
    }

    [Fact]
    public async Task A_workflow_that_gained_a_capability_waits_in_its_own_list_with_the_servers_sentence()
    {
        var dialog = await _f.Opened();

        var own = Assert.Single(dialog.OwnApproval);
        Assert.Equal(("Ship a change", "Ship a change can now open a pull request. Approve it on its own after reading it.", "Newly allowed: Opens or updates a pull request and reads its checks"), (own.Name, own.Text, own.Extra));
        Assert.True(dialog.HasOwnApproval);
    }

    [Fact]
    public async Task An_empty_plan_says_so_and_asks_for_no_challenge()
    {
        _f.Server.ApproveAllPlanAnswer = RouteAnswer.Api("workflow-recipes-approve-all", "empty");

        var dialog = await _f.Opened();

        Assert.Equal((ApproveAllPhase.Empty, "Every recipe is approved.", false), (dialog.Phase, dialog.EmptyText, dialog.CanApprove));
        Assert.Empty(_f.Server.Challenges);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task The_hold_fills_its_bar_only_when_motion_is_not_reduced(bool reduced, bool fills)
    {
        var dialog = await _f.Opened(reducedMotion: reduced);

        dialog.BeginHold();

        Assert.Equal((true, fills, true), (dialog.IsHolding, dialog.ShowsFill, dialog.HoldText.Length > 0));
        dialog.CancelHold();
        Assert.False(dialog.ShowsFill);
    }

    [Fact]
    public async Task A_catalog_that_leaves_approve_all_to_the_terminal_opens_nothing()
    {
        var f = new ApproveAllFixture("extra-approve-all-cli-only");
        var dialog = await f.Dialog();

        await dialog.Open(Token);

        Assert.Equal((false, ApproveAllPhase.Closed, 0), (dialog.IsOpen, dialog.Phase, f.Server.PlanCalls));
    }

    [Fact]
    public async Task A_plan_that_cannot_be_read_fails_in_the_servers_words()
    {
        _f.Server.ApproveAllPlanAnswer = ApproveAllFixture.Refusal("recipes-approve-all", "refused-no-profile");

        var dialog = await _f.Opened();

        Assert.Equal((ApproveAllPhase.Failed, "Approve the profile first, then the recipes.", "SND-SETUP-007"), (dialog.Phase, dialog.Error, dialog.ErrorCode));
    }

    [Fact]
    public async Task An_agent_session_sees_the_plan_and_is_told_in_plain_words_that_only_the_person_can_approve()
    {
        _f.Server.AppChallengeAnswer = ApproveAllFixture.Refusal("challenges-app", "refused-session");

        var dialog = await _f.Opened();

        Assert.Equal((ApproveAllPhase.Plan, Labels.ApproveSessionRefused, "SND-AUTH-002", false), (dialog.Phase, dialog.Error, dialog.ErrorCode, dialog.CanApprove));
        Assert.Equal(2, dialog.Rows.Count);
    }

    [Fact]
    public async Task Holding_for_the_whole_time_answers_with_the_time_held_and_follows_the_job_to_its_end()
    {
        _f.Server.ApproveAllJobAnswers.Enqueue(RouteAnswer.SpecShaped("approve-all-job", "running"));
        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        var sent = Assert.Single(_f.Server.Approvals);
        Assert.Equal(("CHL-0000000000000000000000000000000a", true, null), (sent.Challenge, sent.HeldMs >= 1500, sent.Typed));
        Assert.Equal((ApproveAllPhase.Done, "2 workflows approved", 2), (dialog.Phase, dialog.Outcome, _f.Server.ApproveAllJobCalls));
        Assert.Equal(("Done", false, false), (dialog.CloseLabel, dialog.HasRefused, dialog.HasSkipped));
    }

    [Fact]
    public async Task While_the_job_runs_the_dialog_shows_the_workflow_in_hand()
    {
        _f.Server.ApproveAllJobAnswers.Enqueue(RouteAnswer.SpecShaped("approve-all-job", "running"));
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "running");
        var dialog = await _f.Opened();
        dialog.BeginHold();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1600));

        var answer = dialog.Tick();
        await WaitUntil(() => string.Equals(dialog.Progress, "Approving One step", StringComparison.Ordinal));

        Assert.Equal((ApproveAllPhase.Applying, "Approving One step"), (dialog.Phase, dialog.Progress));
        dialog.Close();
        _f.Clock.Advance(TimeSpan.FromSeconds(1));
        await answer;
    }

    [Fact]
    public async Task Letting_go_early_sends_nothing_and_says_to_hold_again()
    {
        var dialog = await _f.Opened();
        dialog.BeginHold();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(500));

        await dialog.Tick();
        dialog.CancelHold();

        Assert.Equal((false, 0d, Labels.ApproveHoldAgain), (dialog.IsHolding, dialog.HoldProgress, dialog.Notice));
        Assert.Empty(_f.Server.Approvals);
    }

    [Fact]
    public async Task The_fill_follows_the_hold()
    {
        var dialog = await _f.Opened();
        dialog.BeginHold();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(750));

        await dialog.Tick();

        Assert.Equal((0.5d, "Keep holding, 0.8 of 1.5 seconds"), (dialog.HoldProgress, dialog.HoldText));
        Assert.Empty(_f.Server.Approvals);
    }

    [Fact]
    public async Task A_hold_the_server_calls_short_keeps_the_challenge_and_asks_to_hold_again()
    {
        _f.Server.ApproveAllStartAnswer = ApproveAllFixture.Refusal("recipes-approve-all-start", "refused-short-hold");
        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        Assert.Equal((ApproveAllPhase.Plan, "SND-SETUP-003", true, false), (dialog.Phase, dialog.ErrorCode, dialog.CanApprove, dialog.IsSending));
        Assert.StartsWith("Confirmation not given", dialog.Error, StringComparison.Ordinal);
        _ = Assert.Single(_f.Server.Challenges);
    }

    [Fact]
    public async Task Typing_the_challenges_text_answers_with_it_and_not_with_a_time()
    {
        var dialog = await _f.Opened();
        dialog.Typed = " approve-all 1c1fd4 ";

        await _f.Finish(dialog.SubmitTyped());

        var sent = Assert.Single(_f.Server.Approvals);
        Assert.Equal(("approve-all 1c1fd4", null), (sent.Typed, sent.HeldMs));
        Assert.Equal(ApproveAllPhase.Done, dialog.Phase);
    }

    [Fact]
    public async Task Typing_something_else_says_what_to_type_and_uses_up_nothing()
    {
        var dialog = await _f.Opened();
        dialog.Typed = "approve-all c41e08";

        await dialog.SubmitTyped();

        Assert.Equal("That doesn't match. Type exactly: approve-all 1c1fd4", dialog.Notice);
        Assert.Empty(_f.Server.Approvals);
        Assert.True(dialog.CanApprove);
    }

    [Fact]
    public async Task A_plan_that_changed_is_read_again_with_a_new_challenge()
    {
        _f.Server.ApproveAllStartAnswer = ApproveAllFixture.Refusal("recipes-approve-all-start", "refused-plan-changed");
        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        Assert.Equal((ApproveAllPhase.Plan, Labels.ApprovePlanChanged, 2, 2), (dialog.Phase, dialog.Notice, _f.Server.PlanCalls, _f.Server.Challenges.Count));
    }

    [Fact]
    public async Task Any_other_refusal_uses_the_challenge_up_so_a_fresh_one_is_asked_for()
    {
        _f.Server.ApproveAllStartAnswer = ApproveAllFixture.Refusal("recipes-approve-all-start", "refused-running");
        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        Assert.Equal(("SND-JOB-409", 2, true, Labels.ApproveAlreadyRunning), (dialog.ErrorCode, _f.Server.Challenges.Count, dialog.CanApprove, dialog.Error));
    }

    [Fact]
    public async Task A_challenge_that_ran_out_is_asked_for_again_before_a_hold_starts()
    {
        var dialog = await _f.Opened();
        _f.Clock.Advance(TimeSpan.FromMinutes(6));

        dialog.BeginHold();
        await WaitUntil(() => _f.Server.Challenges.Count == 2);

        Assert.Equal((false, string.Empty, true), (dialog.IsHolding, dialog.Notice, dialog.CanApprove));   // the fresh challenge is here, so "that took too long" is over
    }

    [Fact]
    public async Task A_second_answer_while_one_is_on_its_way_is_not_sent()
    {
        _f.Server.ApproveAllGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = await _f.Opened();
        dialog.Typed = "approve-all 1c1fd4";

        var first = dialog.SubmitTyped();
        var second = dialog.SubmitTyped();
        _f.Server.ApproveAllGate.SetResult();
        await _f.Finish(Task.WhenAll(first, second));

        _ = Assert.Single(_f.Server.Approvals);
    }

    [Fact]
    public async Task What_the_checks_refused_and_what_waited_on_it_are_listed_with_the_command_that_approves_it_alone()
    {
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "refused");
        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        Assert.Equal("2 workflows approved", dialog.Outcome);
        var refused = Assert.Single(dialog.Refused);
        Assert.Equal(("Work item", "Work item changed while Sindri was approving. Read the new plan.", "sindri-app recipe approve <id>"), (refused.Name, refused.Text, refused.Cli));
        Assert.Equal("Review loop waits for Work item", Assert.Single(dialog.Skipped).Text);
    }

    [Fact]
    public async Task A_job_that_failed_says_so_in_its_own_words()
    {
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "failed");
        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        Assert.Equal((ApproveAllPhase.Failed, "Sindri stopped while approving.", "SND-JOB-001"), (dialog.Phase, dialog.Error, dialog.ErrorCode));
    }

    [Fact]
    public async Task A_job_that_cannot_be_read_three_times_ends_the_wait_with_words()
    {
        _f.Server.ApproveAllJobAnswer = ApproveAllFixture.Unreadable;
        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        Assert.Equal((ApproveAllPhase.Failed, Labels.ApproveLostTrack, 3), (dialog.Phase, dialog.Error, _f.Server.ApproveAllJobCalls));
    }

    [Fact]
    public async Task Closing_stops_the_dialog_following_the_job()
    {
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "running");
        var dialog = await _f.Opened();
        dialog.BeginHold();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1600));
        var answer = dialog.Tick();
        await WaitUntil(() => _f.Server.ApproveAllJobCalls >= 1);

        dialog.Close();
        _f.Clock.Advance(TimeSpan.FromSeconds(1));
        await answer;

        Assert.Equal((false, ApproveAllPhase.Closed), (dialog.IsOpen, dialog.Phase));
    }

    [Fact]
    public async Task A_send_from_a_closed_dialog_coming_back_does_not_clear_the_new_dialogs_guard()
    {
        var stale = _f.Server.ApproveAllGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = await _f.Opened();
        dialog.Typed = "approve-all 1c1fd4";
        var first = dialog.SubmitTyped();
        dialog.Close();
        await dialog.Open(Token);
        var current = _f.Server.ApproveAllGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialog.Typed = "approve-all 1c1fd4";
        var second = dialog.SubmitTyped();

        stale.SetResult();
        await first;
        var third = dialog.SubmitTyped();

        Assert.Equal((true, 2), (dialog.IsSending, _f.Server.Approvals.Count));
        current.SetResult();
        await _f.Finish(Task.WhenAll(second, third));
    }

    [Fact]
    public async Task A_challenge_that_could_not_be_asked_for_offers_try_again_and_a_retry_gets_it()
    {
        _f.Server.AppChallengeAnswer = ApproveAllFixture.Unreadable;
        var dialog = await _f.Opened();
        Assert.Equal((ApproveAllPhase.Plan, false, true, true), (dialog.Phase, dialog.CanApprove, dialog.CanRetry, dialog.HasError));

        _f.Server.AppChallengeAnswer = RouteAnswer.SpecShaped("challenges-app", "held");
        await dialog.Retry();

        Assert.Equal((true, false, false), (dialog.CanApprove, dialog.CanRetry, dialog.HasError));
    }

    [Fact]
    public async Task A_new_challenge_that_could_not_be_had_keeps_the_refusal_that_asked_for_it_and_offers_try_again()
    {
        _f.Server.ApproveAllStartAnswer = ApproveAllFixture.Refusal("recipes-approve-all-start", "refused-running");
        var dialog = await _f.Opened();
        _f.Server.AppChallengeAnswer = ApproveAllFixture.Unreadable;

        await _f.Hold(dialog);

        Assert.Equal((Labels.ApproveAlreadyRunning, "SND-JOB-409", true, Labels.ApproveAskFailed), (dialog.Error, dialog.ErrorCode, dialog.CanRetry, dialog.Notice));
    }

    [Fact]
    public async Task One_new_challenge_is_asked_for_at_a_time_and_the_ran_out_notice_goes_when_it_arrives()
    {
        var dialog = await _f.Opened();
        _f.Clock.Advance(TimeSpan.FromMinutes(6));
        var gate = _f.Server.AppChallengeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        dialog.BeginHold();
        dialog.BeginHold();
        dialog.BeginHold();
        Assert.Equal(2, _f.Server.Challenges.Count);
        gate.SetResult();
        await WaitUntil(() => dialog.Notice.Length == 0);

        Assert.Equal((true, 2), (dialog.CanApprove, _f.Server.Challenges.Count));
    }

    [Fact]
    public async Task Closing_lets_go_of_a_hold()
    {
        var dialog = await _f.Opened();
        dialog.BeginHold();

        dialog.Close();

        Assert.Equal((false, 0d, false), (dialog.IsHolding, dialog.HoldProgress, dialog.ShowsFill));
    }

    [Fact]
    public async Task A_plan_whose_only_workflows_need_their_own_look_says_so_beside_their_list()
    {
        var plan = JsonNode.Parse(GoldenFiles.Api("workflow-recipes-approve-all", "plan"))!.AsObject();   // why: the golden is a JSON object
        plan["recipes"] = new JsonArray();
        _f.Server.ApproveAllPlanAnswer = new RouteAnswer(HttpStatusCode.OK, plan.ToJsonString());

        var dialog = await _f.Opened();

        Assert.Equal((ApproveAllPhase.Empty, Labels.ApproveOnlyOwn, true, 0), (dialog.Phase, dialog.EmptyText, dialog.HasOwnApproval, _f.Server.Challenges.Count));
    }

    [Fact]
    public async Task The_hold_buttons_help_text_stays_the_instructions_while_the_count_runs()
    {
        var dialog = await _f.Opened();
        var before = dialog.HoldHelp;
        dialog.BeginHold();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(750));

        await dialog.Tick();

        Assert.Equal((Labels.ApproveHold(1500), before, true), (dialog.HoldHelp, dialog.HoldHelp, dialog.HoldText != dialog.HoldHelp));
    }

    [Fact]
    public async Task A_job_that_was_cancelled_says_so_in_words()
    {
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "cancelled");
        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        Assert.Equal((ApproveAllPhase.Failed, Labels.ApproveCancelled), (dialog.Phase, dialog.Error));
    }

    [Fact]
    public async Task A_job_that_runs_for_minutes_is_followed_until_it_ends()
    {
        for (var i = 0; i < 700; i++)   // 700 reads at 500 ms: nearly six minutes
        {
            _f.Server.ApproveAllJobAnswers.Enqueue(RouteAnswer.SpecShaped("approve-all-job", "running"));
        }

        var dialog = await _f.Opened();

        await _f.Hold(dialog);

        Assert.Equal((ApproveAllPhase.Done, 701), (dialog.Phase, _f.Server.ApproveAllJobCalls));
    }

    // The clock does not move on its own: a wait for something the dialog does after a delay is bounded and nudges nothing.
    private static async Task WaitUntil(Func<bool> condition)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(TimeSpan.FromSeconds(20));
        while (!condition())
        {
            await Task.Delay(1, bound.Token);
        }
    }
}
