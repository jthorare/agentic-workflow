using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// '{ loop, below: cap }' (spec §3.5, §3.6, §3.7, #1009): a guard that is false at the cap makes its transition not match, so the first later
/// transition whose 'when' holds is taken; the loop parks with rounds only when the transition taken enters the spent loop.
/// Over the shipped workflow.step, whose 'checks -> produce' on 'failed' is index 2 and whose 'revisions' loop has cap 5.
/// </summary>
public sealed class BelowCapFallThroughTests
{
    private const string Revisions = "revisions";
    private const int Cap = 5;
    private const int Guarded = 2;
    private const int Fallback = 5;

    private static readonly WhenClause _failed = new(OutcomeName.From("failed"), Fact: null, Is: null, Loop: null, Below: null, All: null, Any: null, Not: null);
    private static readonly WhenClause _belowCap = new(Outcome: null, Fact: null, Is: null, Revisions, LoopBound.Cap, All: null, Any: null, Not: null);

    /// <summary>The step recipe, with 'checks -> produce' on 'failed' guarded below the revisions cap (or not) and a labelled 'checks -> done' on 'failed' after it.</summary>
    private static Recipe WithFallback(bool guarded)
    {
        var transitions = Step.Transitions.ToList();
        Assert.Equal(("checks", "produce"), (transitions[Guarded].From[0], transitions[Guarded].To));
        transitions[Guarded] = transitions[Guarded] with
        {
            When = guarded ? new WhenClause(Outcome: null, Fact: null, Is: null, Loop: null, Below: null, All: [_failed, _belowCap], Any: null, Not: null) : _failed,
        };
        transitions.Add(new RecipeTransition(["checks"], "done", "step.completed", _failed, [], [], [], [], Label: "the revisions are spent: finish"));
        return Step with { Transitions = transitions };
    }

    private static WorkflowInstance Checks(Recipe recipe, int round) =>
        At(recipe, "checks") with { Loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { [Revisions] = new LoopState(round, Cap, Last: null) } };

    private static StepCompleted Failed() => Completed(1, "failed", ("fact:findings", "printed nothing"));

    [Theory, Trait("Lane", "unit")]
    [InlineData(Cap - 1, Guarded)]
    [InlineData(Cap, Fallback)]
    public void A_false_below_cap_guard_does_not_match_and_the_next_transition_with_the_outcome_is_taken(int round, int expected)
    {
        var recipe = WithFallback(guarded: true);

        var match = TransitionDecision.Match(Checks(recipe, round), recipe, Failed());

        Assert.Equal(new TransitionMatch(expected, MatchMiss.None), match);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_false_below_cap_guard_applies_the_fallback_transition_and_parks_nothing()
    {
        var recipe = WithFallback(guarded: true);

        var decision = Decide(Checks(recipe, Cap), recipe, Failed(), rules: EngineRules.Default);

        var after = Assert.IsType<Applied>(decision).Changes[0].After;
        Assert.Equal("done", after.State);
        Assert.Null(after.Park);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loop_cap_still_parks_with_rounds_when_the_transition_taken_enters_the_spent_loop()
    {
        var recipe = WithFallback(guarded: false);

        var decision = Decide(Checks(recipe, Cap), recipe, Failed(), rules: EngineRules.Default);

        var after = Assert.IsType<Applied>(decision).Changes[0].After;
        Assert.Equal((EngineStates.Parked, ParkReason.Rounds), (after.State, after.Park));
    }
}
