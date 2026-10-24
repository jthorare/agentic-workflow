using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox's action bar (needs-you spec §3 and §5, slice 2): what A, D and the buttons do per kind of request, and what is sent once.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouBarViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();
    private readonly List<NeedsYouDecisionEventArgs> _decisions = [];
    private Func<NeedsYouDecisionEventArgs, Task<string>> _decisionAnswer = _ => Task.FromResult("");

    private async Task<NeedsYouBarViewModel> Bar(NeedsYouEntry entry, bool read = false)
    {
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api));
        bar.DecisionRequested += (_, e) =>
        {
            _decisions.Add(e);
            e.Settled = _decisionAnswer(e);
        };
        bar.Show(new NeedsYouRowViewModel(entry, seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        bar.ReadToEnd = read;
        return bar;
    }

    // The desk instance's golden with its complete command served in another class (a merge: the line's last step is protected).
    private void ServeProtectedComplete()
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        foreach (var command in instance["current"]!["status"]!["commands"]!.AsArray().Where(c => c!["verb"]!.GetValue<string>() == "complete"))
        {
            command!["class"] = "protected";
        }

        _server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, instance.ToJsonString());
    }

    // The merge stop: the desk instance with its complete command protected, and (when listed) the fact "fact:prStatus" served.
    private void ServeMergeStop(bool listed)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        foreach (var command in instance["current"]!["status"]!["commands"]!.AsArray().Where(c => c!["verb"]!.GetValue<string>() == "complete"))
        {
            command!["class"] = "protected";
        }

        if (listed)
        {
            var row = JsonNode.Parse(instance["facts"]![0]!.ToJsonString())!;
            row["key"] = "fact:prStatus";
            row["type"] = "payload";
            instance["facts"]!.AsArray().Add(row);
        }

        _server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, instance.ToJsonString());
        _server.FactBody = (_, key) => string.Equals(key, "fact:prStatus", StringComparison.Ordinal) ? new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("workflow-instance-fact", "pr-status")) : null;
    }

    [Fact]
    public async Task The_merge_stop_shows_the_pull_request_card_when_the_instance_lists_its_status()
    {
        ServeMergeStop(listed: true);

        var bar = await Bar(NeedsYouRows.Desk());

        Assert.True(bar.HasMergeCard);
        Assert.Equal(("#12", "5 passed, 2 failed, 1 pending"), (bar.MergeCard!.PrText, bar.MergeCard.Lines[0].Value));
    }

    [Fact]
    public async Task A_request_whose_instance_lists_no_status_has_no_card()
    {
        ServeMergeStop(listed: false);

        var bar = await Bar(NeedsYouRows.Desk());

        Assert.False(bar.HasMergeCard);
    }

    [Fact]
    public async Task A_status_that_cannot_be_read_leaves_the_card_out_and_the_request_working()
    {
        ServeMergeStop(listed: true);
        _server.FactBody = (_, _) => new RouteAnswer(System.Net.HttpStatusCode.NotFound, "{}");

        var bar = await Bar(NeedsYouRows.Desk());

        Assert.False(bar.HasMergeCard);
        Assert.NotNull(bar.Hint);
    }

    // The f5 desk golden whose step before the stop recommends an outcome, with the outcome replaced (or dropped).
    private void ServeRecommending(string? outcome)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "f5-desk-recommended"))!;
        var desk = instance["current"]!["desk"]!.AsObject();
        if (outcome is null)
        {
            _ = desk.Remove("recommended");
        }
        else
        {
            desk["recommended"]!["outcome"] = outcome;
        }

        _server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, instance.ToJsonString());
    }

    [Theory]
    [InlineData("approve", true, false)]
    [InlineData("reviseDesign", false, true)]
    [InlineData("done", false, false)]
    [InlineData(null, false, false)]
    public async Task The_recommended_outcome_marks_its_button_suggested_and_its_why_is_the_hint(string? outcome, bool approve, bool sendBack)
    {
        ServeRecommending(outcome);

        var bar = await Bar(NeedsYouRows.Desk());

        Assert.Equal((approve, sendBack), (bar.ApproveSuggested, bar.SendBackSuggested));
        Assert.Equal(approve || sendBack ? "Every check passed and no question is left open." : "", bar.Hint);
        Assert.Equal("Suggested", bar.SuggestedTag);
    }

    // The desk instance of the f5-fields recipe, waiting in one of its states.
    private void ServeDeskIn(string state)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        instance["recipe"]!["id"] = "workflow.f5-fields";
        instance["current"]!["state"] = state;
        _server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, instance.ToJsonString());
    }

    [Fact]
    public async Task A_desk_request_shows_its_states_why_and_what_approving_and_sending_back_do()
    {
        ServeDeskIn("approve");

        var bar = await Bar(NeedsYouRows.Desk());

        Assert.Equal(("You decide whether it goes on.", "Moves on to the hand-over.", "Sends your notes back to the AI.", true, true), (bar.DeskWhy, bar.IfApprove, bar.IfSendBack, bar.HasIfApprove, bar.HasIfSendBack));
    }

    [Fact]
    public async Task A_box_with_no_outcome_text_is_hidden_and_a_state_with_no_why_says_nothing()
    {
        ServeDeskIn("check");

        var bar = await Bar(NeedsYouRows.Desk());

        Assert.Equal(("", "", "", false, false), (bar.DeskWhy, bar.IfApprove, bar.IfSendBack, bar.HasIfApprove, bar.HasIfSendBack));
    }

    [Fact]
    public async Task A_recipe_that_cannot_be_read_leaves_the_texts_empty_and_the_request_working()
    {
        _server.RecipeBody = _ => new RouteAnswer(System.Net.HttpStatusCode.NotFound, "{}");

        var bar = await Bar(NeedsYouRows.Desk());

        Assert.Equal(("", false, false), (bar.DeskWhy, bar.HasIfApprove, bar.HasIfSendBack));
        Assert.True(bar.PrimaryEnabled);
    }

    [Fact]
    public async Task A_on_a_desk_request_not_read_to_the_end_asks_before_approving()
    {
        var bar = await Bar(NeedsYouRows.Desk());

        await bar.PressApprove();

        Assert.Equal((NeedsYouBarMode.Confirming, "Approve?", "You have not read to the end. Press A again to approve."), (bar.Mode, bar.ApproveWord, bar.Hint));
        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task A_second_A_approves_with_the_run_the_instance_serves()
    {
        var bar = await Bar(NeedsYouRows.Desk());

        await bar.PressApprove();
        await bar.PressApprove();

        var (instance, body) = Assert.Single(_server.CompleteCalls);
        Assert.Equal((NeedsYouRows.DeskInstance, "1.1", "approve"), (instance, body.Run, body.Outcome));
        Assert.Equal((NeedsYouBarMode.Approved, true), (bar.Mode, bar.IsApproved));
    }

    [Fact]
    public async Task A_request_read_to_the_end_is_approved_at_once()
    {
        var bar = await Bar(NeedsYouRows.Desk(), read: true);

        await bar.PressApprove();

        Assert.Equal("approve", Assert.Single(_server.CompleteCalls).Body.Outcome);
    }

    [Fact]
    public async Task Reading_to_the_end_clears_the_question_and_showing_another_request_does_too()
    {
        var bar = await Bar(NeedsYouRows.Desk());
        await bar.PressApprove();

        bar.ReadToEnd = true;
        Assert.Equal((NeedsYouBarMode.Pending, "Approve"), (bar.Mode, bar.ApproveWord));

        bar.ReadToEnd = false;
        await bar.PressApprove();
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk("WFI-other"), seen: true, reducedMotion: false, arrived: false, _clock));
        Assert.Equal(NeedsYouBarMode.Pending, bar.Mode);
    }

    [Fact]
    public async Task Two_presses_while_the_report_is_in_flight_send_one()
    {
        _server.CompleteGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bar = await Bar(NeedsYouRows.Desk(), read: true);

        var first = bar.PressApprove();
        await bar.PressApprove();
        _server.CompleteGate.SetResult();
        await first;

        _ = Assert.Single(_server.CompleteCalls);
    }

    [Fact]
    public async Task A_refusal_shows_as_served_and_leaves_the_request_ready_to_press_again()
    {
        _server.CompleteAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Conflict, "SND-WF-040", "That run is not open. Read the instance again.");
        var bar = await Bar(NeedsYouRows.Desk(), read: true);

        await bar.PressApprove();

        Assert.Equal((NeedsYouBarMode.Pending, "That run is not open. Read the instance again."), (bar.Mode, bar.Refusal));
        await bar.PressApprove();
        Assert.Equal(2, _server.CompleteCalls.Count);
    }

    [Fact]
    public async Task D_asks_for_a_reason_and_sends_the_request_back_with_it_as_the_answers_fact()
    {
        var bar = await Bar(NeedsYouRows.Desk());

        await bar.PressSendBack();
        Assert.Equal((NeedsYouBarMode.SendingBack, false), (bar.Mode, bar.CanSend));
        await bar.Send();
        Assert.Empty(_server.CompleteCalls);

        bar.ReasonText = "   ";
        Assert.False(bar.CanSend);
        bar.ReasonText = "cut the scope to expiry only";
        Assert.True(bar.CanSend);
        await bar.Send();

        var body = Assert.Single(_server.CompleteCalls).Body;
        Assert.Equal(("revise", "cut the scope to expiry only"), (body.Outcome, Encoding.UTF8.GetString(body.Outputs["fact:answers"])));
    }

    [Fact]
    public async Task Cancel_closes_the_reason_box_and_sends_nothing()
    {
        var bar = await Bar(NeedsYouRows.Desk());
        await bar.PressSendBack();
        bar.ReasonText = "not now";

        bar.Cancel();

        Assert.Equal((NeedsYouBarMode.Pending, "not now"), (bar.Mode, bar.ReasonText));
        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task Drop_is_only_in_the_more_menu_and_asks_to_confirm_before_it_sends()
    {
        var bar = await Bar(NeedsYouRows.Desk());
        Assert.True(bar.ShowsDrop);

        await bar.RequestDrop();
        Assert.Equal(NeedsYouBarMode.ConfirmingDrop, bar.Mode);
        Assert.Empty(_server.CompleteCalls);

        await bar.ConfirmDrop();
        Assert.Equal("drop", Assert.Single(_server.CompleteCalls).Body.Outcome);
    }

    [Fact]
    public async Task A_desk_request_offers_edit_before_approve_as_a_chip_that_is_not_available_yet()
    {
        Assert.True((await Bar(NeedsYouRows.Desk())).ShowsEditChip);
        Assert.False((await Bar(NeedsYouRows.Decision())).ShowsEditChip);
    }

    [Fact]
    public async Task A_step_that_serves_its_complete_command_as_protected_still_shows_the_approve_button_and_reports_on_a_press()
    {
        ServeProtectedComplete();
        var bar = await Bar(NeedsYouRows.Desk(), read: true);

        Assert.Equal((true, true), (bar.ShowsPrimary, bar.PrimaryEnabled));
        await bar.PressApprove();

        Assert.Equal("approve", Assert.Single(_server.CompleteCalls).Body.Outcome);
    }

    [Fact]
    public async Task A_decision_is_accepted_with_its_recommended_option_and_declined_with_a_reason()
    {
        var bar = await Bar(NeedsYouRows.Decision());

        Assert.Equal(("Accept", "Decline"), (bar.ApproveWord, bar.SendBackWord));
        await bar.PressApprove();
        await bar.PressSendBack();
        bar.ReasonText = "out of scope";
        await bar.Send();

        Assert.Equal([("decision:DEC-1", "accept", ""), ("decision:DEC-1", "decline", "out of scope")], _decisions.Select(d => (d.RowId, d.OptionId, d.Note)));
        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task A_settings_request_can_only_be_approved_so_send_back_is_disabled_with_the_reason()
    {
        var bar = await Bar(NeedsYouRows.Profile());

        Assert.Equal((true, false, "These can only be approved. Change the settings or workflow file to undo."), (bar.ShowsSendBack, bar.SendBackEnabled, bar.SendBackTooltip));
        await bar.PressSendBack();
        Assert.Equal(NeedsYouBarMode.Pending, bar.Mode);
    }

    [Fact]
    public async Task A_park_keeps_the_rows_own_controls_and_has_no_bar_button()
    {
        var bar = await Bar(NeedsYouRows.Grant());

        Assert.Equal((true, false), (bar.ShowsRowControls, bar.ShowsPrimary));
        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task A_failure_keeps_the_rows_own_controls_and_has_no_bar()
    {
        var bar = await Bar(NeedsYouRows.Failure());

        Assert.Equal((true, false, false), (bar.ShowsRowControls, bar.ShowsPrimary, bar.ShowsSendBack));
    }

    [Fact]
    public async Task A_desk_request_with_no_ready_action_sends_nothing()
    {
        var bar = await Bar(NeedsYouRows.Desk() with { Actions = [] });

        await bar.PressApprove();

        Assert.Empty(_server.CompleteCalls);
    }

    [Fact]
    public async Task A_stale_answer_from_the_row_shown_before_does_not_reopen_the_one_sending_now()
    {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"));   // the same desk run served for both instances
        _server.CompleteGate = first;
        var bar = await Bar(NeedsYouRows.Desk(), read: true);
        var stale = bar.PressApprove();

        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk("WFI-other"), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        bar.ReadToEnd = true;
        _server.CompleteGate = second;
        var current = bar.PressApprove();
        first.SetResult();
        await stale;

        Assert.True(bar.IsSending);
        await bar.PressApprove();
        Assert.Equal(2, _server.CompleteCalls.Count);
        second.SetResult();
        await current;
        Assert.Equal(NeedsYouBarMode.Approved, bar.Mode);
    }

    [Fact]
    public async Task A_declined_decision_the_server_refuses_keeps_the_reason_and_shows_the_refusal_in_the_bar()
    {
        _decisionAnswer = _ => Task.FromResult("That decision was already answered.");
        var bar = await Bar(NeedsYouRows.Decision());
        await bar.PressSendBack();
        bar.ReasonText = "out of scope";

        await bar.Send();

        Assert.Equal((NeedsYouBarMode.SendingBack, "out of scope", "That decision was already answered."), (bar.Mode, bar.ReasonText, bar.Refusal));
    }

    [Fact]
    public async Task A_declined_decision_that_was_accepted_closes_the_box_and_clears_the_reason()
    {
        var bar = await Bar(NeedsYouRows.Decision());
        await bar.PressSendBack();
        bar.ReasonText = "out of scope";

        await bar.Send();

        Assert.Equal((NeedsYouBarMode.Pending, "", ""), (bar.Mode, bar.ReasonText, bar.Refusal));
    }

    [Fact]
    public async Task A_pressed_twice_while_the_decision_is_in_flight_answers_it_once()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _decisionAnswer = _ => gate.Task;
        var bar = await Bar(NeedsYouRows.Decision());

        var first = bar.PressApprove();
        await bar.PressApprove();
        gate.SetResult("");
        await first;

        _ = Assert.Single(_decisions);
    }

    [Fact]
    public async Task Send_back_on_a_terminal_only_step_sends_rework_and_on_any_other_revise()
    {
        ServeProtectedComplete();
        var terminal = await Bar(NeedsYouRows.Desk());
        await terminal.PressSendBack();
        terminal.ReasonText = "wrong branch";
        await terminal.Send();

        _server.InstanceBody = null;
        var plain = await Bar(NeedsYouRows.Desk());
        await plain.PressSendBack();
        plain.ReasonText = "cut the scope";
        await plain.Send();

        Assert.Equal(["rework", "revise"], _server.CompleteCalls.Select(c => c.Body.Outcome).ToList());
    }
}
