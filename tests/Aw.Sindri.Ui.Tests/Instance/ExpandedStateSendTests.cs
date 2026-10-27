using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The expanded state sends its Cancel (after the confirm) and its Hand back as ApplyCommand, once per press, and says the answer; with no way to send it says the send arrives later.</summary>
[Lane(TestLane.Unit)]
public sealed class ExpandedStateSendTests
{
    private const string Instance = "WFI-0000000000000000000000000000000a";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private ExpandedStateViewModel Expanded(string scenario, bool wired = true)
    {
        var detail = FixtureInstances.Detail(scenario);
        return new ExpandedStateViewModel(detail.Current, detail.Address, _clipboard, _clock, readPath: (_, _, _) => Task.FromResult<PickedFile?>(new PickedFile("export.txt", 3, "abc"u8.ToArray())))
        {
            Commander = wired ? new InstanceCommander(_server, () => _api) : null,
            Desk = wired ? new DeskReporter(_server, () => _api) : null,
            InstanceId = Instance,
        };
    }

    [Fact]
    public async Task Confirming_the_cancel_sends_ApplyCommand_cancel_once_and_says_so()
    {
        var expanded = Expanded("project-build");
        expanded.AskCancel();

        await expanded.ConfirmCancelAsync();
        await expanded.ConfirmCancelAsync();   // the confirm is gone: a second press sends nothing

        var sent = Assert.Single(_server.CommandCalls);
        Assert.Equal((Instance, CommandKind.Cancel), (sent.Instance, sent.Body.Kind));
        Assert.Equal((false, Labels.CancelSent), (expanded.IsConfirmingCancel, expanded.PendingNote));
    }

    [Fact]
    public async Task Keeping_the_run_sends_nothing()
    {
        var expanded = Expanded("project-build");
        expanded.AskCancel();

        expanded.Keep();
        await expanded.ConfirmCancelAsync();

        Assert.Empty(_server.CommandCalls);
    }

    [Fact]
    public async Task A_refused_cancel_is_said_as_served_and_the_run_can_be_asked_about_again()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-041", "That run has moved on. Read the instance again.");
        var expanded = Expanded("project-build");
        expanded.AskCancel();

        await expanded.ConfirmCancelAsync();
        var said = expanded.PendingNote;
        expanded.AskCancel();

        Assert.Equal(("That run has moved on. Read the instance again.", true), (said, expanded.IsConfirmingCancel));
    }

    [Fact]
    public async Task A_second_press_while_the_cancel_is_out_sends_nothing_more()
    {
        _server.WorkflowCommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expanded = Expanded("project-build");
        expanded.AskCancel();
        var first = expanded.ConfirmCancelAsync();
        expanded.AskCancel();

        await expanded.ConfirmCancelAsync();
        _server.WorkflowCommandGate.SetResult();
        await first;

        _ = Assert.Single(_server.CommandCalls);
    }

    [Fact]
    public async Task Hand_back_sends_ApplyCommand_handback_for_the_instance()
    {
        var expanded = Expanded("project-build");

        await expanded.HandBackAsync();

        var sent = Assert.Single(_server.CommandCalls);
        Assert.Equal((Instance, CommandKind.Handback), (sent.Instance, sent.Body.Kind));
        Assert.Equal(Labels.HandBackSent, expanded.PendingNote);
    }

    [Fact]
    public async Task With_no_way_to_send_the_note_says_the_send_arrives_later()
    {
        var expanded = Expanded("project-build", wired: false);
        expanded.AskCancel();

        await expanded.ConfirmCancelAsync();

        Assert.Equal((Labels.SendingArrivesNote, 0), (expanded.PendingNote, _server.CommandCalls.Count));
    }

    // The f5 desk golden, with the recommended outcome set or dropped.
    private void ServeRecommending(string? outcome)
    {
        var instance = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("workflow-instance", "f5-desk-recommended"))!;
        var desk = instance["current"]!["desk"]!.AsObject();
        desk["outputs"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"key":"fact:output","type":"file","required":true}]""");   // the output the sheet's input stands for
        if (outcome is null)
        {
            _ = desk.Remove("recommended");
        }
        else
        {
            desk["recommended"]!["outcome"] = outcome;
        }

        _server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
    }

    [Fact]
    public void Complete_is_a_button_named_with_its_address_and_carries_no_chip()
    {
        var expanded = Expanded("dev-desk-round-trip");

        var complete = expanded.Commands.Single(c => c.Verb == StatusVerb.Complete);

        Assert.Equal((true, false, string.Empty, $"Complete {FixtureInstances.Detail("dev-desk-round-trip").Address}"), (complete.IsComplete, complete.ShowsLabel, complete.PendingChip, complete.Name));
    }

    [Fact]
    public void Cancel_and_hand_back_carry_no_not_available_chip()
    {
        var expanded = Expanded("project-build");

        Assert.All(expanded.Commands.Where(c => c.IsCancel || c.IsHandBack), c => Assert.Equal(string.Empty, c.PendingChip));
    }

    [Fact]
    public async Task Complete_reports_the_recommended_outcome_once_and_says_so()
    {
        ServeRecommending("approve");
        var expanded = Expanded("dev-desk-round-trip");
        expanded.Outputs.Single().Path = "export.txt";   // the task's required output, picked

        await expanded.CompleteAsync();

        var (instance, body) = Assert.Single(_server.CompleteCalls);
        Assert.Equal((Instance, "approve"), (instance, body.Outcome));
        Assert.Equal(Labels.CompleteSent, expanded.PendingNote);
    }

    [Fact]
    public async Task Complete_reports_the_steps_only_outcome_when_none_is_recommended()
    {
        ServeRecommending(outcome: null);
        var expanded = Expanded("dev-desk-round-trip");
        expanded.Outputs.Single().Path = "export.txt";   // the task's required output, picked

        await expanded.CompleteAsync();

        var (_, body) = Assert.Single(_server.CompleteCalls);
        Assert.Equal(("done", Labels.CompleteSent), (body.Outcome, expanded.PendingNote));
    }

    [Fact]
    public async Task Complete_with_a_recommended_outcome_that_needs_a_reason_sends_nothing_and_asks_for_the_choice()
    {
        ServeRecommending("reviseDesign");
        var expanded = Expanded("dev-desk-round-trip");

        await expanded.CompleteAsync();

        Assert.Empty(_server.CompleteCalls);
        Assert.Equal(Labels.CompleteNeedsChoice, expanded.PendingNote);
    }
}
