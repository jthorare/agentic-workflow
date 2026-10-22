using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
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
        return new ExpandedStateViewModel(detail.Current, detail.Address, _clipboard, _clock)
        {
            Commander = wired ? new InstanceCommander(_server, () => _api) : null,
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
}
