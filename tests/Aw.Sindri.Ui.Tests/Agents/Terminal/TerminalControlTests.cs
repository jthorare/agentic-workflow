using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>Stop (two presses) and Interrupt in the pane header, for a session Sindri started only (#3023, #3334).</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalControlTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void A_session_Sindri_did_not_start_shows_neither_Stop_nor_Interrupt()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        Assert.False(f.View.CanStop);
        Assert.False(f.View.CanInterrupt);
    }

    [Fact]
    public void A_session_Sindri_started_shows_both_while_the_pane_is_live()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started());

        Assert.True(f.View.CanStop);
        Assert.True(f.View.CanInterrupt);
    }

    [Fact]
    public void Interrupt_is_not_offered_when_there_is_no_live_pane_but_Stop_stays_in_Take_it_back()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started());
        f.View.SocketClosed(4001);

        Assert.False(f.View.CanInterrupt);
        Assert.True(f.View.CanStop);
    }

    [Fact]
    public async Task The_first_press_only_asks_and_the_label_says_press_again()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started());
        Assert.Equal(("Stop…", "TerminalStop"), (f.View.StopLabel, f.View.StopAutomationId));

        var stopped = await f.View.PressStop(Token);

        Assert.False(stopped);
        Assert.Empty(f.Control.Stopped);
        Assert.Equal(("Stop? Press again", "TerminalStopConfirm", true), (f.View.StopLabel, f.View.StopAutomationId, f.View.IsStopArmed));
    }

    [Fact]
    public async Task The_second_press_inside_2_seconds_stops_the_session()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started("qa"));
        _ = await f.View.PressStop(Token);
        f.Clock.Advance(TimeSpan.FromSeconds(1.9));

        var stopped = await f.View.PressStop(Token);

        Assert.True(stopped);
        Assert.Equal(["session:claude-code/qa"], f.Control.Stopped);
        Assert.False(f.View.IsStopArmed);
    }

    [Fact]
    public async Task A_second_press_after_2_seconds_asks_again_and_stops_nothing()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started());
        _ = await f.View.PressStop(Token);
        f.Clock.Advance(TimeSpan.FromSeconds(2.1));

        var stopped = await f.View.PressStop(Token);

        Assert.False(stopped);
        Assert.Empty(f.Control.Stopped);
        Assert.True(f.View.IsStopArmed);
    }

    [Fact]
    public async Task Tick_after_2_seconds_puts_the_label_back()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started());
        _ = await f.View.PressStop(Token);
        f.Clock.Advance(TimeSpan.FromSeconds(2.1));

        f.View.Tick();

        Assert.Equal(("Stop…", false), (f.View.StopLabel, f.View.IsStopArmed));
    }

    [Fact]
    public async Task Switching_to_another_row_drops_the_ask()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started("a"));
        _ = await f.View.PressStop(Token);

        f.View.SwitchTo(TerminalFixture.Started("b"));

        Assert.False(f.View.IsStopArmed);
    }

    [Fact]
    public async Task A_refused_stop_says_so_and_a_session_that_is_not_ours_is_never_stopped()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started());
        f.Control.Refuse = true;
        _ = await f.View.PressStop(Token);

        var stopped = await f.View.PressStop(Token);

        Assert.False(stopped);
        Assert.Equal("Couldn't stop it.", f.View.ControlLine);
        var lane = new TerminalFixture().Attached(TerminalFixture.Lane());
        _ = await lane.View.PressStop(Token);
        _ = await lane.View.PressStop(Token);
        Assert.Empty(lane.Control.Stopped);
    }

    [Fact]
    public async Task Interrupt_sends_Ctrl_C_to_that_session_once_per_press()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Started("qa"));

        var sent = await f.View.PressInterrupt(Token);

        Assert.True(sent);
        Assert.Equal(["session:claude-code/qa"], f.Control.Interrupted);
    }

    [Fact]
    public async Task Interrupt_on_a_session_that_is_not_ours_sends_nothing()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        var sent = await f.View.PressInterrupt(Token);

        Assert.False(sent);
        Assert.Empty(f.Control.Interrupted);
    }
}
