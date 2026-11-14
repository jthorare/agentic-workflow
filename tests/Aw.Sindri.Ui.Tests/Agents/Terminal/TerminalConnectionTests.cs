using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>The header words and the Connection rules (agents spec §15.3): retries, the 30 s rule, a second view, keys before `ready`.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalConnectionTests
{
    [Fact]
    public void The_state_word_follows_the_socket()
    {
        var f = new TerminalFixture();

        f.View.Open(TerminalFixture.Lane(), TerminalOpenIntent.Look);
        var connecting = f.View.StateWord;
        _ = f.View.Ready();
        var attached = f.View.StateWord;

        Assert.Equal(("Connecting", "Attached"), (connecting, attached));
    }

    [Fact]
    public void A_one_off_says_Starting_until_the_agent_reports_a_state()
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.OneOff(), TerminalOpenIntent.Look);
        _ = f.View.Ready();
        var starting = f.View.StateWord;

        f.View.AgentReportedState();

        Assert.Equal(("Starting", "Attached"), (starting, f.View.StateWord));
    }

    [Fact]
    public void A_drop_returns_focus_to_the_list_and_dims_the_screen()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        f.View.SocketDropped();

        Assert.Equal((TerminalSide.List, TerminalState.Reconnecting, true), (f.View.Side, f.View.State, f.View.IsDimmed));
    }

    [Fact]
    public void Retries_come_after_1_2_4_and_8_seconds_then_every_15()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());
        f.View.SocketDropped();
        var waits = new List<double?> { f.View.RetryIn?.TotalSeconds };

        foreach (var _ in Enumerable.Range(0, 5))
        {
            f.Clock.Advance(f.View.RetryIn!.Value);
            f.View.Tick();
            f.View.SocketDropped();
            waits.Add(f.View.RetryIn?.TotalSeconds);
        }

        Assert.Equal([1d, 2d, 4d, 8d, 15d, 15d], waits);
        Assert.Equal(5, f.Requests);
    }

    [Fact]
    public void After_30_seconds_without_a_socket_the_word_is_Cant_reach_Sindri_with_Retry_now()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());
        f.View.SocketDropped();

        f.Clock.Advance(TimeSpan.FromSeconds(31));
        f.View.Tick();

        Assert.Equal(("Can't reach Sindri", true, "Esc closes the view"), (f.View.StateWord, f.View.ShowRetryNow, f.View.HintText));
    }

    [Fact]
    public void Retry_now_asks_for_a_socket_at_once()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());
        f.View.SocketDropped();
        f.Clock.Advance(TimeSpan.FromSeconds(31));
        f.View.Tick();
        var before = f.Requests;

        f.View.RetryNow();

        Assert.Equal((before + 1, TerminalState.Connecting), (f.Requests, f.View.State));
    }

    [Fact]
    public void Close_code_4001_shows_the_other_window_message_and_does_not_retry()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        f.View.SocketClosed(4001);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        f.View.Tick();

        Assert.Equal(("This pane is open in another Sindri window", true, TerminalSide.List, 0), (f.View.StateWord, f.View.ShowTakeItBack, f.View.Side, f.Requests));
    }

    [Fact]
    public void Take_it_back_opens_a_new_socket()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());
        f.View.SocketClosed(4001);

        f.View.TakeItBack();

        Assert.Equal((1, TerminalState.Connecting, false), (f.Requests, f.View.State, f.View.ShowTakeItBack));
    }

    [Fact]
    public void Close_code_4016_says_Gone()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        f.View.SocketClosed(4016);

        Assert.Equal(("Gone", TerminalSide.List, "tmux session gone"), (f.View.StateWord, f.View.Side, f.View.CardReason));
    }

    [Fact]
    public void Keys_before_ready_wait_up_to_one_second_then_release_in_order()
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Lane(), TerminalOpenIntent.Type);
        f.View.SocketOpening();

        var fates = new[] { f.View.SendKey("a"), f.View.SendKey("b") };
        f.Clock.Advance(TimeSpan.FromMilliseconds(400));
        var released = f.View.Ready();

        Assert.Equal([TerminalKeyFate.Queued, TerminalKeyFate.Queued], fates);
        Assert.Equal(["a", "b"], released);
    }

    [Fact]
    public void Keys_that_waited_over_a_second_are_dropped_and_the_header_word_flashes()
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Lane(), TerminalOpenIntent.Type);
        f.View.SocketOpening();
        _ = f.View.SendKey("a");

        f.Clock.Advance(TimeSpan.FromMilliseconds(1100));
        f.View.Tick();
        var released = f.View.Ready();

        Assert.Empty(released);
        Assert.Equal(1, f.View.FlashCount);
    }

    [Fact]
    public void A_key_while_the_socket_is_down_is_dropped()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);
        f.View.SocketDropped();

        Assert.Equal(TerminalKeyFate.Dropped, f.View.SendKey("a"));
    }

    [Fact]
    public void The_header_shows_the_socket_kind_and_the_other_clients()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        f.View.SetOtherClients(1);
        var one = f.View.HostText;
        f.View.SetOtherClients(0);

        Assert.Equal(("tmux default · 1 other client", "tmux default"), (one, f.View.HostText));
    }

    [Fact]
    public void Close_code_1008_is_final_with_its_reason_and_does_not_retry()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        f.View.SocketClosed(1008);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        f.View.Tick();

        Assert.Equal((null, 0, TerminalSide.List), (f.View.RetryIn, f.Requests, f.View.Side));
        Assert.Contains("token", f.View.CardBody, StringComparison.OrdinalIgnoreCase);
    }
}
