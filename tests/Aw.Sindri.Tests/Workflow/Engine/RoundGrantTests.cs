using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// '--grant n' and '--text' on a loop park (spec W7, §3.8, §5.1, §5.8): the arithmetic and the bounds. The caller class
/// (a grant needs a terminal) is checked by the command chain, and the resume by the frame rules.
/// </summary>
public sealed class RoundGrantTests
{
    [Fact, Trait("Lane", "unit")]
    public void Grant_adds_n_rounds_and_needs_a_terminal()
    {
        var parked = LoopParked(ParkReason.Rounds, new LoopState(3, 3, Findings("B")));

        var granted = LoopRules.Grant(parked, StepRecipe, 2).Value;

        Assert.Equal(new LoopState(3, 5, Last: null), granted.Loops[Revisions]);
        Assert.Equal(parked.Suspension, granted.Suspension);
        Assert.Equal(parked.Frame, granted.Frame);
        Assert.Equal("SND-WF-003", LoopRules.Grant(parked, StepRecipe, 0).Error.Code);
        Assert.Equal("SND-WF-003", LoopRules.Grant(parked, StepRecipe, -1).Error.Code);
        Assert.True(LoopRules.Grant(parked, StepRecipe, 3).IsSuccess);
        Assert.Equal(new LoopState(3, 1_003, Last: null), LoopRules.Grant(parked, StepRecipe, 1_000).Value.Loops[Revisions]);   // owner q93: no upper bound
    }

    [Fact, Trait("Lane", "unit")]
    public void The_round_a_grant_starts_is_the_first_of_the_n()
    {
        var parked = LoopParked(ParkReason.Rounds, new LoopState(3, 3, Findings("B")));
        var granted = Popped(LoopRules.Grant(parked, StepRecipe, 2).Value);

        // The resume performs the withheld 'checks -> produce': round 4, the first of the 2 granted.
        var fourth = LoopRules.Reenter(granted, StepRecipe, "produce", Findings("C"));
        Assert.Equal(new LoopState(4, 5, Findings("C")), fourth.Next);
        var fifth = LoopRules.Reenter(granted with { Loops = With(fourth.Next!) }, StepRecipe, "produce", Findings("D")); // why: asserted just above
        Assert.Equal(new LoopState(5, 5, Findings("D")), fifth.Next);
        var sixth = LoopRules.Reenter(granted with { Loops = With(fifth.Next!) }, StepRecipe, "produce", Findings("E")); // why: asserted just above

        Assert.Equal(ParkReason.Rounds, sixth.Park?.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_grant_on_no_progress_starts_n_rounds_from_the_current_one()
    {
        var parked = LoopParked(ParkReason.NoProgress, new LoopState(2, 3, Findings("A")));

        var granted = LoopRules.Grant(parked, StepRecipe, 1).Value;
        var resumed = LoopRules.Reenter(Popped(granted), StepRecipe, "produce", Findings("A"));

        Assert.Equal(new LoopState(2, 3, Last: null), granted.Loops[Revisions]);
        Assert.Equal(new LoopState(3, 3, Findings("A")), resumed.Next);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_grant_on_a_refusal_kind_park_uses_the_active_states_loop()
    {
        var parked = Parked(In("produce", new LoopState(3, 3, Failure("F"))), ParkReason.Rounds, ParkKind.Refusal, ResumeKind.Rerun);

        var granted = LoopRules.Grant(parked, StepRecipe, 1).Value;

        Assert.Equal(new LoopState(3, 4, Last: null), granted.Loops[Revisions]);
        Assert.False(LoopRules.AtCap(Popped(granted), StepRecipe, "produce"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_grant_fits_only_a_rounds_or_no_progress_park()
    {
        var loop = new LoopState(3, 3, Last: null);

        Assert.Equal("SND-WF-003", LoopRules.Grant(In("produce", loop), StepRecipe, 1).Error.Code);
        Assert.Equal("SND-WF-003", LoopRules.Grant(Parked(In("produce", loop), ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun), StepRecipe, 1).Error.Code);
        Assert.Equal("SND-WF-003", LoopRules.Grant(Popped(LoopParked(ParkReason.Rounds, loop)), StepRecipe, 1).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_on_no_progress_works_again_on_the_next_park()
    {
        var parked = LoopParked(ParkReason.NoProgress, new LoopState(2, 3, Findings("A")));

        var answered = LoopRules.AnswerText(parked, StepRecipe).Value;
        var resumed = LoopRules.Reenter(Popped(answered), StepRecipe, "produce", Findings("A"));
        Assert.Equal(new LoopState(2, 3, Last: null) { TextAnswered = true }, answered.Loops[Revisions]);
        Assert.Equal(3, resumed.Next?.Round);

        var parkedAgain = LoopParked(ParkReason.NoProgress, resumed.Next! with { Last = Findings("A") }); // why: asserted just above
        var again = LoopRules.AnswerText(parkedAgain, StepRecipe);

        Assert.Equal(new LoopState(3, 4, Last: null) { TextAnswered = true }, again.Value.Loops[Revisions]);   // owner q93: a second text answer resumes again
        Assert.True(LoopRules.Grant(parkedAgain, StepRecipe, 1).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_at_the_cap_still_runs_the_round_it_starts()
    {
        var parked = LoopParked(ParkReason.NoProgress, new LoopState(3, 3, Findings("A")));

        var answered = LoopRules.AnswerText(parked, StepRecipe).Value;

        Assert.Equal(new LoopState(3, 4, Last: null) { TextAnswered = true }, answered.Loops[Revisions]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_on_a_rounds_park_is_refused()
    {
        var refused = LoopRules.AnswerText(LoopParked(ParkReason.Rounds, new LoopState(3, 3, Last: null)), StepRecipe);

        Assert.Equal("SND-WF-003", refused.Error.Code);
        Assert.Contains("--grant", refused.Error.Message, StringComparison.Ordinal);
    }

    private static Dictionary<string, LoopState> With(LoopState loop) => new(StringComparer.Ordinal) { [Revisions] = loop };
}
