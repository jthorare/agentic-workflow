using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>An ended session freezes the pane (agents spec §15.3 "A row whose session ends"), and the can't-attach card (§15.5).</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalEndedTests
{
    [Fact]
    public void A_dead_frame_freezes_the_pane_dimmed_and_moves_focus_to_the_list()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane(), TerminalOpenIntent.Type);

        f.View.FrameReceived(["last screen"], dead: true);

        Assert.Equal((TerminalState.Ended, true, TerminalSide.List, "last screen"), (f.View.State, f.View.IsDimmed, f.View.Side, f.View.Rows[0]));
        Assert.Equal("Ended 14:00", f.View.EndedText);
    }

    [Fact]
    public void An_ended_lane_offers_Copy_resume_command_and_nothing_to_remove()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());

        f.View.SessionEnded();

        Assert.Equal((true, false, false), (f.View.CanCopyResume, f.View.CanRemoveWorktree, f.View.CanDiscardSession));
    }

    [Fact]
    public void An_ended_one_off_with_a_worktree_offers_Remove_worktree_and_Discard_session()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.OneOff("abcd1234", "/wt/abcd1234"));
        f.View.SessionEnded();

        Assert.Equal((true, true), (f.View.CanRemoveWorktree, f.View.CanDiscardSession));
        f.View.RemoveWorktree();
        f.View.DiscardSession();
        Assert.Equal(["abcd1234"], f.RemovedWorktrees);
        Assert.Equal(["abcd1234"], f.Discarded);
    }

    [Fact]
    public void An_ended_one_off_without_a_worktree_has_no_Remove_worktree()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.OneOff());

        f.View.SessionEnded();

        Assert.Equal((false, true), (f.View.CanRemoveWorktree, f.View.CanDiscardSession));
    }

    [Fact]
    public void Close_leaves_the_session_alone()
    {
        var f = new TerminalFixture().Attached(TerminalFixture.OneOff());
        f.View.SessionEnded();

        f.View.Close();

        Assert.Equal((1, 0), (f.Closes, f.Discarded.Count));
    }

    [Fact]
    public void The_cant_attach_card_has_the_reason_the_last_screen_and_both_buttons()
    {
        var f = new TerminalFixture();

        f.View.Open(TerminalFixture.Warp(), TerminalOpenIntent.Look);

        Assert.Equal(
            ("Warp tab: Sindri can't show or type into this terminal.", "last line of the transcript", true, true),
            (f.View.CardReason, f.View.CardTail, f.View.CanJump, f.View.CanCopyResume));
        Assert.Equal(TerminalSide.List, f.View.Side);
    }
}
