using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>
/// The loop-cap rules (spec §3.7, §9.1, A §17.23.13): RCP-LOOP-001 (an undeclared loop, or a cap below 1),
/// RCP-LOOP-002 (a looped state that isn't on a cycle) and RCP-LOOP-003 (an empty noProgress).
/// </summary>
public sealed class LoopRulesTests
{
    private static readonly (string, LoopSpec)[] _revisions = [("revisions", new LoopSpec(3, LoopSpec.DefaultNoProgress))];

    [Fact, Trait("Lane", "unit")]
    public void A_state_in_an_undeclared_loop_is_refused()
    {
        // The recipe of invalid/RCP-LOOP-001.yaml; 'loop: revision' is at 9:5.
        var recipe = Of(
            "produce",
            [("produce", Active(loop: "revision")), ("done", Terminal())],
            [Go("produce", "done"), Go("produce", "produce", "step.failed")],
            _revisions,
            id: "workflow.loop-undeclared");

        Assert.Equal(
            ["RCP-LOOP-001: state 'produce' names loop 'revision', which isn't declared at RCP-LOOP-001.yaml:9:5 (states.produce.loop); declare it under loops, or name a declared loop"],
            Lines(recipe, "RCP-LOOP-001.yaml", [("states.produce.loop", 9, 5)]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_loop_without_a_cap_of_at_least_one_is_refused(int cap)
    {
        var recipe = Of(
            "produce",
            [("produce", Active(loop: "revisions")), ("done", Terminal())],
            [Go("produce", "done"), Go("produce", "produce", "step.failed")],
            [("revisions", new LoopSpec(cap, LoopSpec.DefaultNoProgress))]);

        Assert.Equal(
            [$"RCP-LOOP-001: loop 'revisions' has cap {cap.ToString(CultureInfo.InvariantCulture)} at r.yaml:1:1 (loops.revisions.cap); set cap to 1 or more"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_of_step_transitions_with_no_looped_state_is_refused()
    {
        // workflow.step without 'loop: revisions': produce -> checks -> produce runs without a round cap.
        var recipe = Of(
            "produce",
            [("produce", Active()), ("checks", Active()), ("done", Terminal())],
            [Go("produce", "checks"), Go("checks", "produce"), Go("checks", "done")]);

        Assert.Equal(
            ["RCP-LOOP-001: the cycle through 'produce', 'checks' has no loop cap at r.yaml:1:1 (states.produce); add loop: <name> to one of its states and declare the loop under loops with a cap"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_that_retries_itself_on_failure_without_a_loop_is_refused()
    {
        var recipe = Of(
            "produce",
            [("produce", Active()), ("done", Terminal())],
            [Go("produce", "done"), Go("produce", "produce", "step.failed")]);

        Assert.Equal(
            ["RCP-LOOP-001: the cycle through 'produce' has no loop cap at r.yaml:1:1 (states.produce); add loop: <name> to one of its states and declare the loop under loops with a cap"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_closed_by_a_command_or_through_a_looped_state_is_capped()
    {
        var byCommand = Of(
            "produce",
            [("produce", Active()), ("checks", Active()), ("done", Terminal())],
            [Go("produce", "checks"), Go("checks", "produce", "command.retry"), Go("checks", "done")]);
        var looped = Of(
            "produce",
            [("produce", Active(loop: "revisions")), ("checks", Active()), ("done", Terminal())],
            [Go("produce", "checks"), Go("checks", "produce"), Go("checks", "done")],
            _revisions);

        Assert.Empty(Lines(byCommand));
        Assert.Empty(Lines(looped));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_closed_by_a_human_answer_is_capped_like_a_command()
    {
        // A clarification loop: a human answers each round, so no loop cap is needed (owner decision, 2026-10-11).
        var recipe = Of(
            "work",
            [("work", Active()), ("ask", WaitForAnswer()), ("done", Terminal())],
            [Go("work", "done"), Go("work", "ask", "step.failed"), Go("ask", "work", "wait.satisfied")]);

        Assert.Empty(Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_through_an_event_wait_still_needs_a_loop()
    {
        var recipe = Of(
            "work",
            [("work", Active()), ("hold", WaitForEvent("item.routed")), ("done", Terminal())],
            [Go("work", "done"), Go("work", "hold", "step.failed"), Go("hold", "work", "wait.satisfied")]);

        Assert.Equal(
            ["RCP-LOOP-001: the cycle through 'work', 'hold' has no loop cap at r.yaml:1:1 (states.work); add loop: <name> to one of its states and declare the loop under loops with a cap"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_looped_state_that_is_not_on_a_cycle_is_refused()
    {
        // The recipe of invalid/RCP-LOOP-002.yaml: nothing leads back into 'produce'.
        var recipe = Of(
            "produce",
            [("produce", Active(loop: "revisions")), ("done", Terminal())],
            [Go("produce", "done")],
            _revisions,
            id: "workflow.loop-no-cycle");

        Assert.Equal(
            ["RCP-LOOP-002: state 'produce' is in loop 'revisions' but no transition leads back into it, so the loop never counts a second round at RCP-LOOP-002.yaml:9:5 (states.produce.loop); add the transition that leads back into it, or remove loop"],
            Lines(recipe, "RCP-LOOP-002.yaml", [("states.produce.loop", 9, 5)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loop_with_an_empty_no_progress_list_is_refused()
    {
        // The recipe of invalid/RCP-LOOP-003.yaml; 'noProgress: []' is at 18:31.
        var recipe = Of(
            "produce",
            [("produce", Active(loop: "revisions")), ("done", Terminal())],
            [Go("produce", "done"), Go("produce", "produce", "step.failed")],
            [("revisions", new LoopSpec(3, []))],
            id: "workflow.loop-no-signal");

        Assert.Equal(
            ["RCP-LOOP-003: loop 'revisions' has an empty noProgress at RCP-LOOP-003.yaml:18:31 (loops.revisions.noProgress); name at least one of findings, emptyDiff and sameFailure, or leave noProgress out to use all three"],
            Lines(recipe, "RCP-LOOP-003.yaml", [("loops.revisions.noProgress", 18, 31)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loop_closed_through_other_states_raises_nothing()
    {
        // workflow.step's shape (spec §12): checks and verify both lead back into the looped 'produce'.
        var recipe = Of(
            "produce",
            [("produce", Active(loop: "revisions", next: "Produce the Step output for {item} (round {loop.revisions}/{loop.revisions.cap}).")),
             ("checks", Active()), ("verify", Active()), ("done", Terminal())],
            [Go("produce", "checks"), Go("checks", "verify"), Go("checks", "produce"), Go("verify", "done"), Go("verify", "produce")],
            [("revisions", new LoopSpec(3, [NoProgressSignal.Findings]))],
            id: "workflow.step");

        Assert.Empty(Lines(recipe));
    }
}
