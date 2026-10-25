using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.Tests.Approvals;

/// <summary>The approve-all dialog (setup-once spec §4, §5.3): the plan in words, one plain Approve click with no challenge, the job followed.</summary>
[Lane(TestLane.Unit)]
public sealed class ApproveAllViewModelTests
{
    private readonly ApproveAllFixture _f = new();

    private static CancellationToken Token => ApproveAllFixture.Token;

    [Fact]
    public async Task Open_reads_the_plan_and_offers_one_click_to_approve_it()
    {
        var dialog = await _f.Opened();

        Assert.Equal((ApproveAllPhase.Plan, true, "2 workflows will be approved"), (dialog.Phase, dialog.IsOpen, dialog.Heading));
        Assert.True(dialog.CanApprove);
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
    public async Task An_empty_plan_says_so_and_offers_nothing_to_approve()
    {
        _f.Server.ApproveAllPlanAnswer = RouteAnswer.Api("workflow-recipes-approve-all", "empty");

        var dialog = await _f.Opened();

        Assert.Equal((ApproveAllPhase.Empty, "Every recipe is approved.", false), (dialog.Phase, dialog.EmptyText, dialog.CanApprove));
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
        _f.Server.ApproveAllStartAnswer = RouteAnswer.Problem(HttpStatusCode.Forbidden, "SND-AUTH-002", "An agent session cannot approve.");
        var dialog = await _f.Opened();

        await _f.Approve(dialog);

        Assert.Equal((ApproveAllPhase.Plan, Labels.ApproveSessionRefused, "SND-AUTH-002"), (dialog.Phase, dialog.Error, dialog.ErrorCode));
        Assert.Equal(2, dialog.Rows.Count);
    }

    [Fact]
    public async Task One_click_approves_with_no_challenge_and_follows_the_job_to_its_end()
    {
        _f.Server.ApproveAllJobAnswers.Enqueue(RouteAnswer.SpecShaped("approve-all-job", "running"));
        var dialog = await _f.Opened();

        await _f.Approve(dialog);

        _ = Assert.Single(_f.Server.Approvals);
        Assert.Equal((ApproveAllPhase.Done, "2 workflows approved", 2), (dialog.Phase, dialog.Outcome, _f.Server.ApproveAllJobCalls));
        Assert.Equal(("Done", false, false), (dialog.CloseLabel, dialog.HasRefused, dialog.HasSkipped));
    }

    [Fact]
    public async Task While_the_job_runs_the_dialog_shows_the_workflow_in_hand()
    {
        _f.Server.ApproveAllJobAnswers.Enqueue(RouteAnswer.SpecShaped("approve-all-job", "running"));
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "running");
        var dialog = await _f.Opened();
        var answer = dialog.Approve();
        await WaitUntil(() => string.Equals(dialog.Progress, "Approving One step", StringComparison.Ordinal));

        Assert.Equal((ApproveAllPhase.Applying, "Approving One step"), (dialog.Phase, dialog.Progress));
        dialog.Close();
        _f.Clock.Advance(TimeSpan.FromSeconds(1));
        await answer;
    }

    [Fact]
    public async Task A_plan_that_changed_is_read_again()
    {
        _f.Server.ApproveAllStartAnswer = ApproveAllFixture.Refusal("recipes-approve-all-start", "refused-plan-changed");
        var dialog = await _f.Opened();

        await _f.Approve(dialog);

        Assert.Equal((ApproveAllPhase.Plan, Labels.ApprovePlanChanged, 2), (dialog.Phase, dialog.Notice, _f.Server.PlanCalls));
    }

    [Fact]
    public async Task A_second_answer_while_one_is_on_its_way_is_not_sent()
    {
        _f.Server.ApproveAllGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = await _f.Opened();

        var first = dialog.Approve();
        var second = dialog.Approve();
        _f.Server.ApproveAllGate.SetResult();
        await _f.Finish(Task.WhenAll(first, second));

        _ = Assert.Single(_f.Server.Approvals);
    }

    [Fact]
    public async Task What_the_checks_refused_and_what_waited_on_it_are_listed_with_the_command_that_approves_it_alone()
    {
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "refused");
        var dialog = await _f.Opened();

        await _f.Approve(dialog);

        Assert.Equal("2 workflows approved", dialog.Outcome);
        var refused = Assert.Single(dialog.Refused);
        Assert.Equal(("Work item", "Work item changed while Sindri was approving. Read the new plan.", "sindri recipe approve <id>"), (refused.Name, refused.Text, refused.Cli));
        Assert.Equal("Review loop waits for Work item", Assert.Single(dialog.Skipped).Text);
    }

    [Fact]
    public async Task A_job_that_failed_says_so_in_its_own_words()
    {
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "failed");
        var dialog = await _f.Opened();

        await _f.Approve(dialog);

        Assert.Equal((ApproveAllPhase.Failed, "Sindri stopped while approving.", "SND-JOB-001"), (dialog.Phase, dialog.Error, dialog.ErrorCode));
    }

    [Fact]
    public async Task A_job_that_cannot_be_read_three_times_ends_the_wait_with_words()
    {
        _f.Server.ApproveAllJobAnswer = ApproveAllFixture.Unreadable;
        var dialog = await _f.Opened();

        await _f.Approve(dialog);

        Assert.Equal((ApproveAllPhase.Failed, Labels.ApproveLostTrack, 3), (dialog.Phase, dialog.Error, _f.Server.ApproveAllJobCalls));
    }

    [Fact]
    public async Task Closing_stops_the_dialog_following_the_job()
    {
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "running");
        var dialog = await _f.Opened();
        var answer = dialog.Approve();
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
        var first = dialog.Approve();
        dialog.Close();
        await dialog.Open(Token);
        var current = _f.Server.ApproveAllGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = dialog.Approve();

        stale.SetResult();
        await first;
        var third = dialog.Approve();

        Assert.Equal((true, 2), (dialog.IsSending, _f.Server.Approvals.Count));
        current.SetResult();
        await _f.Finish(Task.WhenAll(second, third));
    }

    [Fact]
    public async Task A_plan_whose_only_workflows_need_their_own_look_says_so_beside_their_list()
    {
        var plan = JsonNode.Parse(GoldenFiles.Api("workflow-recipes-approve-all", "plan"))!.AsObject();   // why: the golden is a JSON object
        plan["recipes"] = new JsonArray();
        _f.Server.ApproveAllPlanAnswer = new RouteAnswer(HttpStatusCode.OK, plan.ToJsonString());

        var dialog = await _f.Opened();

        Assert.Equal((ApproveAllPhase.Empty, Labels.ApproveOnlyOwn, true), (dialog.Phase, dialog.EmptyText, dialog.HasOwnApproval));
    }

    [Fact]
    public async Task A_job_that_was_cancelled_says_so_in_words()
    {
        _f.Server.ApproveAllJobAnswer = RouteAnswer.SpecShaped("approve-all-job", "cancelled");
        var dialog = await _f.Opened();

        await _f.Approve(dialog);

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

        await _f.Approve(dialog);

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
