using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The run's row when it waits for a free session slot (states.json 'waiting'): a word, never red, said once, then "Started".</summary>
[Lane(TestLane.Unit)]
public sealed class ExpandedStateSandboxTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static StatusFields Waiting(int running = 3) =>
        FixtureInstances.Parse<StatusFields>(WithRunning(JsonNode.Parse(GoldenFiles.SpecShaped("workflow-status", "sandbox-waiting-for-slot"))!.AsObject(), running));

    // The same run once its session started: no wait on the run, and the session it started.
    private static StatusFields Started()
    {
        var status = Waiting();
        return status with { Run = status.Run! with { WaitingForSlot = null, Session = new SessionHandle("claude", "abc") } };
    }

    // The wait ended with no session: the launch was cancelled or failed.
    private static StatusFields EndedWithoutSession()
    {
        var status = Waiting();
        return status with { Run = status.Run! with { WaitingForSlot = null } };
    }

    // The golden with the running count the test wants.
    private static JsonObject WithRunning(JsonObject status, int running)
    {
        status["run"]!["waitingForSlot"]!["running"] = running;
        return status;
    }

    private ExpandedStateViewModel Expanded(StatusFields status) =>
        new(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, new FakeClipboard(), _clock);

    [Fact]
    public void A_run_waiting_on_the_cap_says_how_many_are_running()
    {
        var expanded = Expanded(Waiting());

        Assert.Equal(("Waiting for a free session slot (3 of 3 running)", true), (expanded.WaitingLine, expanded.HasWaiting));
    }

    [Fact]
    public void The_wait_is_said_once_in_the_live_region()
    {
        var expanded = Expanded(Waiting());

        expanded.AnnounceWaiting();
        expanded.AnnounceWaiting();

        Assert.Equal(("Waiting for a free session slot, 3 of 3 running", true), (expanded.WaitingVoice.Announcement, expanded.WaitingVoice.IsAnnouncing));
    }

    [Fact]
    public void A_count_that_changes_changes_the_line_but_does_not_say_it_again()
    {
        var expanded = Expanded(Waiting());
        expanded.AnnounceWaiting();
        var said = new List<string>();
        expanded.WaitingVoice.PropertyChanged += (_, e) => said.Add(e.PropertyName ?? string.Empty);

        expanded.Update(new CurrentState("produce", Step: null, Gates: [], Desk: null, Waiting(running: 2)));

        Assert.Equal(("Waiting for a free session slot (2 of 3 running)", 0), (expanded.WaitingLine, said.Count));
    }

    [Fact]
    public void When_the_wait_ends_the_row_says_Started_once()
    {
        var expanded = Expanded(Waiting());
        expanded.AnnounceWaiting();

        expanded.Update(new CurrentState("produce", Step: null, Gates: [], Desk: null, Started()));

        Assert.Equal(("Started", "Started", true), (expanded.WaitingLine, expanded.WaitingVoice.Announcement, expanded.HasWaiting));
    }

    [Fact]
    public void A_run_that_never_waited_says_nothing()
    {
        var expanded = Expanded(Started());

        expanded.AnnounceWaiting();

        Assert.Equal((string.Empty, false, false), (expanded.WaitingLine, expanded.HasWaiting, expanded.WaitingVoice.IsAnnouncing));
    }

    [Fact]
    public void A_wait_that_ends_without_a_session_clears_the_line_and_never_says_Started()
    {
        var expanded = Expanded(Waiting());
        expanded.AnnounceWaiting();

        expanded.Update(new CurrentState("produce", Step: null, Gates: [], Desk: null, EndedWithoutSession()));

        Assert.Equal((string.Empty, "Waiting for a free session slot, 3 of 3 running"), (expanded.WaitingLine, expanded.WaitingVoice.Announcement));
    }
}
