using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>RCP-REACH-001 (every state is reachable) and RCP-EXIT-001 (every non-terminal state has a way out), spec §9.1.</summary>
public sealed class ReachAndExitRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Unreachable_state_is_reported_with_its_location()
    {
        // The recipe of invalid/RCP-REACH-001.yaml; 'orphan' starts at 14:3.
        var recipe = Of(
            "produce",
            [("produce", Active()), ("done", Terminal()), ("orphan", Terminal("unresolved"))],
            [Go("produce", "done")],
            id: "workflow.reach");

        Assert.Equal(
            ["RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a transition into it or remove it"],
            Lines(recipe, "RCP-REACH-001.yaml", [("states.orphan", 14, 3)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_that_only_loops_on_itself_has_no_way_out()
    {
        // The recipe of invalid/RCP-EXIT-001.yaml: checks sends itself back to checks and never ends.
        var recipe = Of(
            "produce",
            [("produce", Active()), ("checks", Active()), ("done", Terminal())],
            [Go("produce", "done"), Go("produce", "checks", "step.failed"), Go("checks", "checks")],
            id: "workflow.exit");

        Assert.Equal(
            ["RCP-EXIT-001: no transition from state 'checks' leads to a terminal state at RCP-EXIT-001.yaml:11:3 (states.checks); add a transition from it, or from a state it leads to, into a terminal state"],
            Lines(recipe, "RCP-EXIT-001.yaml", [("states.checks", 11, 3)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_state_of_a_trap_cycle_has_no_way_out()
    {
        // checks and verify send each other back and forth and never end.
        var recipe = Of(
            "produce",
            [("produce", Active()), ("checks", Active()), ("verify", Active()), ("done", Terminal())],
            [Go("produce", "done"), Go("produce", "checks", "step.failed"), Go("checks", "verify"), Go("verify", "checks")]);

        Assert.Equal(
            [
                "RCP-EXIT-001: no transition from state 'checks' leads to a terminal state at r.yaml:1:1 (states.checks); add a transition from it, or from a state it leads to, into a terminal state",
                "RCP-EXIT-001: no transition from state 'verify' leads to a terminal state at r.yaml:1:1 (states.verify); add a transition from it, or from a state it leads to, into a terminal state",
            ],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_with_no_transition_out_has_no_way_out()
    {
        var recipe = Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "produce", "step.failed")]);

        Assert.Equal(
            ["RCP-EXIT-001: no transition from state 'produce' leads to a terminal state at r.yaml:1:1 (states.produce); add a transition from it, or from a state it leads to, into a terminal state",
             "RCP-REACH-001: state 'done' is unreachable from 'produce' at r.yaml:1:1 (states.done); add a transition into it or remove it"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_reached_through_any_transition_source_is_reachable()
    {
        var recipe = Of(
            "triaged",
            [("triaged", WaitForEvent("item.routed")), ("startable", WaitForEvent("item.routed")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [
                Go("triaged", "startable", "wait.satisfied"),
                Go("startable", "done", "wait.satisfied"),
                Go("startable", "dropped", "wait.satisfied") with { From = ["triaged", "startable"] },
            ]);

        Assert.Empty(Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_minimal_recipe_raises_nothing() =>
        Assert.Empty(Lines(ARecipe.Minimal(RecipeId.From("workflow.minimal"))));

    [Fact, Trait("Lane", "unit")]
    public void A_missing_initial_or_target_state_is_left_to_the_schema_rules()
    {
        var noInitial = ARecipe.Minimal(RecipeId.From("workflow.minimal")) with { Initial = "nowhere" };
        var noTarget = Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "done"), Go("produce", "nowhere", "step.failed")]);

        Assert.Empty(Lines(noInitial));
        Assert.Empty(Lines(noTarget));
    }
}
