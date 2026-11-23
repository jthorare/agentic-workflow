using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>Switching the pane to another row (agents spec §15.3 "Switching", D15): focus stays in the list, the cached screen shows dimmed until a live frame lands.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalSwitchTests
{
    [Fact]
    public void Switching_keeps_focus_in_the_list_even_when_the_pane_had_it()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane("ideas"), TerminalOpenIntent.Type);

        f.View.SwitchTo(TerminalFixture.Lane("qa"));

        Assert.Equal((TerminalSide.List, "qa"), (f.View.Side, f.View.HeaderName));
    }

    [Fact]
    public void Switching_back_paints_the_cached_screen_dimmed_until_the_first_live_frame()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane("ideas"));
        f.View.FrameReceived(["ideas screen"], dead: false);
        f.View.SwitchTo(TerminalFixture.Lane("qa"));
        _ = f.View.Ready();
        f.View.FrameReceived(["qa screen"], dead: false);

        f.View.SwitchTo(TerminalFixture.Lane("ideas"));
        var cached = (f.View.Rows[0], f.View.IsDimmed);
        _ = f.View.Ready();
        f.View.FrameReceived(["ideas live"], dead: false);

        Assert.Equal(("ideas screen", true), cached);
        Assert.Equal(("ideas live", false), (f.View.Rows[0], f.View.IsDimmed));
    }

    [Fact]
    public void A_row_never_shown_has_no_cached_screen()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane("ideas"));

        f.View.SwitchTo(TerminalFixture.Lane("qa"));

        Assert.Empty(f.View.Rows);
    }

    [Fact]
    public void The_cache_keeps_the_last_8_sessions()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane("s0"));
        for (var i = 1; i <= 9; i++)
        {
            f.View.SwitchTo(TerminalFixture.Lane("s" + i.ToString(CultureInfo.InvariantCulture)));
            _ = f.View.Ready();
            f.View.FrameReceived(["screen " + i.ToString(CultureInfo.InvariantCulture)], dead: false);
        }

        f.View.SwitchTo(TerminalFixture.Lane("s0"));

        Assert.Equal(8, f.View.CachedScreenCount);
        Assert.Empty(f.View.Rows);
    }

    [Fact]
    public void Switching_to_a_row_without_a_pane_shows_the_card_with_no_socket_wanted()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane("ideas"));
        var requests = f.Requests;

        f.View.SwitchTo(TerminalFixture.Warp());

        Assert.Equal((TerminalState.CantAttach, "Can't attach", requests), (f.View.State, f.View.StateWord, f.Requests));
    }
}
