using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>Where focus starts and moves in the terminal view, and what the footer and borders say (agents spec §15.3, decisions D14 and D17 fed by it).</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalFocusTests
{
    [Fact]
    public void Enter_on_a_lane_opens_the_pane_with_focus_in_the_list()
    {
        var f = new TerminalFixture();

        f.View.Open(TerminalFixture.Lane(), TerminalOpenIntent.Look);

        Assert.Equal(TerminalSide.List, f.View.Side);
        Assert.Equal("Enter again, or F6, to type in ideas · Esc close · n new", f.View.FooterText);
    }

    [Fact]
    public void A_one_off_just_started_opens_with_focus_in_the_pane()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.OneOff(), TerminalOpenIntent.Look);

        Assert.Equal(TerminalSide.Pane, f.View.Side);
    }

    [Fact]
    public void The_type_intent_opens_a_lane_with_focus_in_the_pane()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        Assert.Equal(TerminalSide.Pane, f.View.Side);
    }

    [Fact]
    public void Enter_again_on_the_shown_row_moves_focus_into_the_pane()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        f.View.PressEnterOnShownRow();

        Assert.Equal(TerminalSide.Pane, f.View.Side);
    }

    [Fact]
    public void A_click_in_the_pane_moves_focus_in_and_a_click_on_the_list_takes_it_back()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        f.View.ClickPane();
        var inPane = f.View.Side;
        f.View.ClickList();

        Assert.Equal((TerminalSide.Pane, TerminalSide.List), (inPane, f.View.Side));
    }

    [Theory]
    [InlineData("F6")]
    [InlineData("Shift+F6")]
    [InlineData("Ctrl+Shift+L")]
    public void Each_chord_swaps_the_side_and_is_not_sent_to_the_agent(string chord)
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        var toPane = f.View.Route(chord);
        var side = f.View.Side;
        var back = f.View.Route(chord);

        Assert.Equal((TerminalKeyRoute.Handled, TerminalSide.Pane, TerminalKeyRoute.Handled, TerminalSide.List), (toPane, side, back, f.View.Side));
    }

    [Fact]
    public void On_a_mac_the_second_chord_is_Cmd_Shift_L_and_Ctrl_Shift_L_reaches_the_agent()
    {
        var f = new TerminalFixture(isMac: true).Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        var cmd = f.View.Route("Cmd+Shift+L");
        var ctrl = f.View.Route("Ctrl+Shift+L");

        Assert.Equal((TerminalKeyRoute.Handled, TerminalSide.List, TerminalKeyRoute.Page), (cmd, f.View.Side, ctrl));
    }

    [Theory]
    [InlineData("Esc")]
    [InlineData("Ctrl+K")]
    [InlineData("G")]
    [InlineData("j")]
    public void With_focus_in_the_pane_every_other_key_goes_to_the_agent(string key)
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        Assert.Equal(TerminalKeyRoute.Pane, f.View.Route(key));
        Assert.False(f.View.ShellKeysEnabled);
        Assert.False(f.View.PaletteEnabled);
        Assert.Equal(0, f.Closes);
    }

    [Fact]
    public void With_focus_in_the_list_Esc_closes_the_view_and_other_keys_go_to_the_page()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        var j = f.View.Route("j");
        var esc = f.View.Route("Esc");

        Assert.Equal((TerminalKeyRoute.Page, TerminalKeyRoute.Handled, 1), (j, esc, f.Closes));
        Assert.True(f.View.ShellKeysEnabled);
        Assert.True(f.View.PaletteEnabled);
    }

    [Fact]
    public void A_chord_cannot_move_focus_into_a_pane_that_is_not_live()
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Lane(), TerminalOpenIntent.Look);

        _ = f.View.Route("F6");

        Assert.Equal(TerminalSide.List, f.View.Side);
    }

    [Fact]
    public void The_footer_changes_with_focus_and_names_the_chords_for_the_platform()
    {
        var linux = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);
        var mac = new TerminalFixture(isMac: true).Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        Assert.Equal("Typing goes to ideas · F6 or Ctrl+Shift+L back to the list · Shift+PgUp scroll back", linux.View.FooterText);
        Assert.Equal("Typing goes to ideas · F6 or Cmd+Shift+L back to the list · Shift+PgUp scroll back", mac.View.FooterText);
        _ = mac.View.Route("F6");
        Assert.Equal("Enter again, F6 or Cmd+Shift+L to type in ideas · Esc close · n new", mac.View.FooterText);
    }

    [Fact]
    public void The_focused_side_has_the_accent_and_the_unfocused_cursor_is_hollow()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        var list = (f.View.IsListAccented, f.View.IsPaneAccented, f.View.IsCursorHollow);
        f.View.ClickPane();
        var pane = (f.View.IsListAccented, f.View.IsPaneAccented, f.View.IsCursorHollow);

        Assert.Equal(((true, false, true), (false, true, false)), (list, pane));
    }

    [Fact]
    public void A_modal_takes_the_keys_and_gives_focus_back_where_it_was()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        f.View.OpenModal();
        var during = f.View.Route("x");
        f.View.CloseModal();

        Assert.Equal((TerminalKeyRoute.Page, TerminalSide.Pane), (during, f.View.Side));
    }
}
