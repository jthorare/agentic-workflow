using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A run the user took over (needs-you spec §3, §4, slice 4): the clock text from the served time left, the nudge from the served row, and Hand back.</summary>
[Lane(TestLane.Unit)]
public sealed class TakeoverViewModelTests
{
    private const string Instance = NeedsYouRows.DeskInstance;
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();

    private TakeoverViewModel Takeover() => new(_server, () => _api, new InstanceCommander(_server, () => _api), _clock);

    // The desk instance's golden, served as a taken-over run with this much time left (or none).
    private void ServeTakenOver(long? left)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        instance["current"]!["status"]!["paused"] = left is { } seconds ? new JsonObject { ["timeoutLeftSeconds"] = seconds } : null;
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
    }

    [Fact]
    public async Task The_takeover_line_says_when_the_run_was_taken_over_from_the_served_time()
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        instance["current"]!["status"]!["paused"] = new JsonObject { ["timeoutLeftSeconds"] = 10200 };
        instance["current"]!["status"]!["takenOverAt"] = (A.Noon - TimeSpan.FromMinutes(155)).ToString("O", System.Globalization.CultureInfo.InvariantCulture);   // 09:25, 2h 35m before the clock
        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
        var takeover = Takeover();

        await takeover.Load(Instance);

        Assert.Equal("you took over at 09:25 (2h 35m ago)", takeover.TookOverLine);
    }

    [Fact]
    public async Task A_takeover_with_no_served_time_has_no_line()
    {
        ServeTakenOver(left: 10200);
        var takeover = Takeover();

        await takeover.Load(Instance);

        Assert.Equal((false, ""), (takeover.HasTookOverLine, takeover.TookOverLine));
    }

    private static NeedsYouEntry NudgeRow(TimeSpan age, string instance = Instance) => NeedsYouRows.Park("takeover:" + instance) with
    {
        Label = "taken over",
        PrimaryAction = "ApplyCommand:handback",
        Since = A.Noon - age,
    };

    [Fact]
    public async Task Clock_reads_the_served_time_left()
    {
        ServeTakenOver(left: 10200);   // 2h 50m
        var takeover = Takeover();

        await takeover.Load(Instance);

        Assert.Equal(("You took over", "clock paused, 2h 50m left"), (takeover.Badge, takeover.ClockText));
        Assert.Equal("Hand back when you are done. The workflow carries on from here, and the clock restarts with the 2h 50m that was left.", takeover.HandBackBox);
    }

    [Fact]
    public async Task A_takeover_with_no_time_served_says_only_that_the_clock_is_paused()
    {
        _server.InstanceBody = null;   // the plain golden serves no paused line
        var takeover = Takeover();

        await takeover.Load(Instance);

        Assert.Equal((false, "clock paused"), (takeover.IsTakenOver, takeover.Footer));
    }

    [Fact]
    public async Task The_nudge_shows_from_the_takeover_row_and_Not_yet_hides_it_until_a_new_row()
    {
        ServeTakenOver(left: 10200);
        var takeover = Takeover();
        await takeover.Load(Instance);

        takeover.Observe([NeedsYouRows.Park(), NudgeRow(TimeSpan.FromHours(3))]);
        Assert.Equal((true, "You've had this for 3 hours: hand it back?"), (takeover.NudgeVisible, takeover.NudgeTitle));

        takeover.NotYet();
        takeover.Observe([NudgeRow(TimeSpan.FromHours(3))]);   // the same row served again
        Assert.False(takeover.NudgeVisible);

        takeover.Observe([]);
        takeover.Observe([NudgeRow(TimeSpan.FromMinutes(1))]);   // a new row
        Assert.True(takeover.NudgeVisible);
    }

    [Fact]
    public async Task The_nudge_of_another_instance_does_not_show()
    {
        ServeTakenOver(left: 10200);
        var takeover = Takeover();
        await takeover.Load(Instance);

        takeover.Observe([NudgeRow(TimeSpan.FromHours(3), instance: "WFI-other")]);

        Assert.False(takeover.NudgeVisible);
    }

    [Fact]
    public async Task Hand_back_sends_only_ApplyCommand_handback_then_runs_the_clock_and_shows_the_toast_for_five_seconds()
    {
        ServeTakenOver(left: 10200);
        var takeover = Takeover();
        await takeover.Load(Instance);
        takeover.Observe([NudgeRow(TimeSpan.FromHours(3))]);

        await takeover.HandBack();

        var (instance, body) = Assert.Single(_server.CommandCalls);
        Assert.Equal((Instance, CommandKind.Handback, null, null), (instance, body.Kind, body.Answer, body.Text));
        Assert.Empty(_server.CompleteCalls);
        Assert.Equal(("Running", "clock running, 2h 50m left", false, true), (takeover.Badge, takeover.Footer, takeover.NudgeVisible, takeover.ToastVisible));

        _clock.Advance(TimeSpan.FromSeconds(5));
        await takeover.ToastExpiry;
        Assert.False(takeover.ToastVisible);
    }

    [Fact]
    public async Task Pressing_Hand_back_twice_while_it_is_sending_sends_once()
    {
        ServeTakenOver(left: 10200);
        var takeover = Takeover();
        await takeover.Load(Instance);
        _server.WorkflowCommandGate = new TaskCompletionSource();

        var first = takeover.HandBack();
        await takeover.HandBack();
        _server.WorkflowCommandGate.SetResult();
        await first;

        _ = Assert.Single(_server.CommandCalls);
    }

    [Fact]
    public async Task A_refused_hand_back_shows_the_served_words_and_stays_taken_over()
    {
        ServeTakenOver(left: 10200);
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-041", "That run has moved on. Read the instance again.");
        var takeover = Takeover();
        await takeover.Load(Instance);

        await takeover.HandBack();

        Assert.Equal((true, "That run has moved on. Read the instance again.", false), (takeover.IsTakenOver, takeover.Refusal, takeover.ToastVisible));
    }

    [Fact]
    public async Task Cancel_run_asks_first_and_the_confirm_sends_once()
    {
        ServeTakenOver(left: 10200);
        var takeover = Takeover();
        await takeover.Load(Instance);

        takeover.AskCancel();
        Assert.Equal((true, 0), (takeover.IsAskingCancel, _server.CommandCalls.Count));

        await takeover.ConfirmCancel();
        await takeover.ConfirmCancel();   // the question is gone: a second press does nothing

        var (instance, body) = Assert.Single(_server.CommandCalls);
        Assert.Equal((Instance, CommandKind.Cancel, false), (instance, body.Kind, takeover.IsAskingCancel));
    }

    [Fact]
    public async Task Backing_out_of_the_cancel_question_sends_nothing()
    {
        ServeTakenOver(left: 10200);
        var takeover = Takeover();
        await takeover.Load(Instance);

        takeover.AskCancel();
        takeover.BackOutOfCancel();
        await takeover.ConfirmCancel();

        Assert.Equal((false, 0), (takeover.IsAskingCancel, _server.CommandCalls.Count));
    }
}
