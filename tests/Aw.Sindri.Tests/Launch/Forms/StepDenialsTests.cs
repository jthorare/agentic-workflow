using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>What an agent Step is denied (LN-LCH1): the other worktrees beside its own, and the holdout store.</summary>
public sealed class StepDenialsTests
{
    private const string Own = "/repo/.claude/worktrees/sindri-W-1";

    [Fact, Trait("Lane", "unit")]
    public void Sibling_worktrees_are_denied_but_the_Steps_own_is_not()
    {
        var denied = StepDenials.For(Own, ["/repo/.claude/worktrees/sindri-W-0", Own, "/repo/.claude/worktrees/sindri-line-p2"], sindriHome: null);

        Assert.Equal(["/repo/.claude/worktrees/sindri-W-0", "/repo/.claude/worktrees/sindri-line-p2"], denied, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_trailing_slash_on_the_own_worktree_still_matches() => Assert.Empty(StepDenials.For(Own + "/", [Own], sindriHome: null));

    [Fact, Trait("Lane", "unit")]
    public void The_holdout_store_under_the_Sindri_home_is_denied()
    {
        var denied = StepDenials.For(Own, [], "/home/u/.sindri");

        Assert.Equal(["/home/u/.sindri/holdout"], denied, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_nothing_known_nothing_is_denied() =>
        Assert.Empty(StepDenials.For(Own, [], sindriHome: null));
}
