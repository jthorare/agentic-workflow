using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>Card states have buttons and no pane focus (agents spec §15.3 "Card buttons"), and the header rules of #3400.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalCardTests
{
    private static TerminalFixture CantReach()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());
        f.View.SocketDropped();
        f.Clock.Advance(TimeSpan.FromSeconds(31));
        f.View.Tick();
        return f;
    }

    [Fact]
    public void F6_in_a_card_state_focuses_the_first_button_never_a_pane()
    {
        var f = CantReach();

        _ = f.View.Route("F6");

        Assert.Equal((TerminalSide.Card, 0, false), (f.View.Side, f.View.FocusedButton, f.View.IsPaneAccented));
        Assert.Equal("Enter to Retry now · F6 back to the list", f.View.FooterText);
    }

    [Fact]
    public void Enter_on_the_shown_row_and_a_click_on_the_card_also_focus_the_first_button()
    {
        var a = CantReach();
        var b = CantReach();

        a.View.PressEnterOnShownRow();
        b.View.ClickPane();

        Assert.Equal((TerminalSide.Card, TerminalSide.Card), (a.View.Side, b.View.Side));
    }

    [Fact]
    public void The_list_footer_of_a_card_state_names_the_first_button() =>
        Assert.Equal("F6 to Retry now · Esc close · n new", CantReach().View.FooterText);

    [Fact]
    public void Tab_and_Shift_Tab_move_between_the_ended_banner_buttons_and_wrap()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.OneOff("abcd1234", "/wt/x"));
        f.View.SessionEnded();
        _ = f.View.Route("F6");

        _ = f.View.Route("Tab");
        var second = f.View.FocusedButton;
        _ = f.View.Route("Tab");
        _ = f.View.Route("Tab");
        var wrapped = f.View.FocusedButton;
        _ = f.View.Route("Shift+Tab");

        Assert.Equal([TerminalCardButton.CopyResume, TerminalCardButton.RemoveWorktree, TerminalCardButton.DiscardSession], f.View.CardButtons);
        Assert.Equal((1, 0, 2), (second, wrapped, f.View.FocusedButton));
    }

    [Fact]
    public void Enter_presses_the_focused_button()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.OneOff("abcd1234", "/wt/x"));
        f.View.SessionEnded();
        _ = f.View.Route("F6");
        _ = f.View.Route("Tab");
        _ = f.View.Route("Tab");

        var route = f.View.Route("Enter");

        Assert.Equal(TerminalKeyRoute.Handled, route);
        Assert.Equal(["abcd1234"], f.Discarded);
    }

    [Fact]
    public void Enter_on_Retry_now_asks_for_a_socket_and_returns_focus_to_the_list()
    {
        var f = CantReach();
        var before = f.Requests;
        _ = f.View.Route("F6");

        _ = f.View.Route("Enter");

        Assert.Equal((before + 1, TerminalSide.List), (f.Requests, f.View.Side));
    }

    [Theory]
    [InlineData("Esc")]
    [InlineData("F6")]
    [InlineData("Ctrl+Shift+L")]
    public void Esc_and_the_chords_take_focus_back_to_the_list_from_the_card(string key)
    {
        var f = CantReach();
        _ = f.View.Route("F6");

        _ = f.View.Route(key);

        Assert.Equal((TerminalSide.List, 0), (f.View.Side, f.Closes));
    }

    [Fact]
    public void Reconnecting_has_no_button_so_F6_does_nothing_and_the_footer_says_so()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());
        f.View.SocketDropped();

        _ = f.View.Route("F6");

        Assert.Equal((TerminalSide.List, "Reconnecting · Esc close · n new"), (f.View.Side, f.View.FooterText));
    }

    [Fact]
    public void The_cant_attach_card_has_Jump_then_Copy_and_never_a_pane()
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Warp(), TerminalOpenIntent.Type);

        f.View.ClickPane();

        Assert.Equal([TerminalCardButton.Jump, TerminalCardButton.CopyResume], f.View.CardButtons);
        Assert.Equal(TerminalSide.Card, f.View.Side);
    }

    [Fact]
    public void Take_it_back_keeps_Stop_and_Cant_reach_and_Cant_attach_drop_it()
    {
        var taken = new TerminalFixture().Attached(TerminalFixture.Lane());
        taken.View.SocketClosed(4001);
        var warp = new TerminalFixture();
        warp.View.Open(TerminalFixture.Warp(), TerminalOpenIntent.Look);

        Assert.Equal((true, false, false), (taken.View.ShowStop, CantReach().View.ShowStop, warp.View.ShowStop));
    }

    [Fact]
    public void Ended_and_Cant_reach_drop_the_other_client_count()
    {
        var ended = new TerminalFixture().Attached(TerminalFixture.Lane());
        ended.View.SetOtherClients(1);
        ended.View.SessionEnded();
        var lost = CantReach();
        lost.View.SetOtherClients(1);

        Assert.Equal(("tmux default", "tmux default"), (ended.View.HostText, lost.View.HostText));
    }
}
