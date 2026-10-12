using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>RCP-WHEN-001/002 (a 'when' fits its trigger's source) and RCP-OUTCOME-001 (every source outcome is handled; terminals use W6's closed set), spec §1 W6, §3.6.</summary>
public sealed class OutcomeRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_outcome_the_Step_doesnt_report_is_reported()
    {
        // The recipe of invalid/RCP-WHEN-001.yaml.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done")), Go("produce", "done", when: Outcome("finished"))], id: "workflow.when-outcome");

        Assert.Equal(
            ["RCP-WHEN-001: transition from 'produce' tests outcome 'finished', which step 'step.produce' doesn't report at RCP-WHEN-001.yaml:16:60 (transitions[1].when.outcome); test one of its outcomes: done"],
            CheckWiring(recipe, new Located("RCP-WHEN-001.yaml", [("transitions[1].when.outcome", 16, 60)])).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void Outcomes_nested_in_all_any_and_not_come_from_the_source_too()
    {
        var recipe = Of("triaged", [("triaged", WaitForEvent("item.routed")), ("done", Terminal())],
            [
                Go("triaged", "done", "wait.satisfied", AnyOf(Outcome("startable"), Outcome("routed"))),
                Go("triaged", "done", "wait.satisfied", new WhenClause(Outcome: null, Fact: null, Is: null, Loop: null, Below: null, All: null, Any: null, Not: Outcome("notStartable"))),
            ]);

        Assert.Equal(
            ["RCP-WHEN-001: transition from 'triaged' tests outcome 'routed', which event 'item.routed' doesn't report at r.yaml:1:1 (transitions[0].when.any[1].outcome); test one of its outcomes: startable, notStartable"],
            CheckWiring(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_reports_its_terminal_outcomes_and_cancelled()
    {
        var recipe = Of("running", [("running", WaitForChild("workflow.step")), ("done", Terminal()), ("dropped", Terminal("unresolved"))],
            [
                Go("running", "done", "wait.satisfied", Outcome("resolved")),
                Go("running", "dropped", "wait.satisfied", AnyOf(Outcome("cancelled"), Outcome("escalated"))),
            ]);
        var children = new InMemoryChildRecipes().With(Child(Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"))], id: "workflow.step")));

        Assert.Equal(
            ["RCP-WHEN-001: transition from 'running' tests outcome 'escalated', which child 'workflow.step' doesn't report at r.yaml:1:1 (transitions[1].when.any[1].outcome); test one of its outcomes: resolved, cancelled"],
            CheckWiring(recipe, Located.Nowhere with { Children = children }).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loop_the_recipe_doesnt_declare_is_reported()
    {
        // The recipe of invalid/RCP-WHEN-002.yaml.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: AllOf(Outcome("done"), BelowCap("rounds")))], id: "workflow.when-loop");

        Assert.Equal(
            ["RCP-WHEN-002: transition from 'produce' tests loop 'rounds', which the recipe doesn't declare at RCP-WHEN-002.yaml:15:87 (transitions[0].when.all[1].loop); declare it under loops, or test a declared loop"],
            CheckWiring(recipe, new Located("RCP-WHEN-002.yaml", [("transitions[0].when.all[1].loop", 15, 87)])).Lines());
        Assert.Empty(CheckWiring(recipe with { Loops = new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["rounds"] = new(2, LoopSpec.DefaultNoProgress) } }));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("startable", "wait.satisfied", "resource 'slot'")]
    [InlineData("produce", "step.failed", "trigger 'step.failed'")]
    [InlineData("produce", "command.retry", "trigger 'command.retry'")]
    public void An_outcome_test_on_a_trigger_without_outcomes_is_reported(string from, string on, string source)
    {
        var recipe = Of("produce", [("produce", Active("step.produce")), ("startable", WaitForResource(ResourceKind.Slot)), ("done", Terminal())],
            [Go("produce", "startable", when: Outcome("done")), Go("startable", "done", "wait.satisfied"), Go(from, "done", on, Outcome("ready"))]);

        Assert.Equal(
            [$"RCP-WHEN-002: transition from '{from}' tests outcome 'ready', but {source} reports no outcome at r.yaml:1:1 (transitions[2].when.outcome); remove the outcome test, since this trigger has none"],
            CheckWiring(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_answer_wait_reports_answered()
    {
        var recipe = Of("asked", [("asked", WaitForAnswer("go")), ("done", Terminal())],
            [Go("asked", "done", "wait.satisfied", Outcome("approved"))]);

        Assert.Equal(
            [
                "RCP-OUTCOME-001: state 'asked' has no transition for outcome 'answered' of answer 'go' at r.yaml:1:1 (states.asked); add a transition from 'asked' on wait.satisfied with when: { outcome: answered }",
                "RCP-WHEN-001: transition from 'asked' tests outcome 'approved', which answer 'go' doesn't report at r.yaml:1:1 (transitions[0].when.outcome); test one of its outcomes: answered",
            ],
            CheckWiring(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Step_outcome_with_no_transition_is_reported()
    {
        // The recipe of invalid/RCP-OUTCOME-001.yaml: step.checks reports 'failed', and nothing handles it.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("checks", Active("step.checks")), ("done", Terminal())],
            [Go("produce", "checks", when: Outcome("done"), produces: ["fact:output"]), Go("checks", "done", when: Outcome("passed"))],
            id: "workflow.outcome");

        Assert.Equal(
            ["RCP-OUTCOME-001: state 'checks' has no transition for outcome 'failed' of step 'step.checks' at RCP-OUTCOME-001.yaml:11:3 (states.checks); add a transition from 'checks' on step.completed with when: { outcome: failed }"],
            CheckWiring(recipe, new Located("RCP-OUTCOME-001.yaml", [("states.checks", 11, 3)])).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_without_an_outcome_test_handles_every_outcome()
    {
        var recipe = Of("produce", [("produce", Active("step.produce")), ("checks", Active("step.checks")), ("done", Terminal())],
            [
                Go("produce", "checks", when: Outcome("done"), produces: ["fact:output"]),
                Go("checks", "produce", when: AllOf(Outcome("failed"), BelowCap("revisions"))),
                Go("checks", "done"),
            ],
            loops: ["revisions"]);

        Assert.Empty(CheckWiring(recipe));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("finished")]
    [InlineData("cancelled")]
    public void A_terminal_outside_the_closed_set_is_reported(string outcome)
    {
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal(outcome))], [Go("produce", "done", when: Outcome("done"))]);

        Assert.Equal(
            [$"RCP-OUTCOME-001: terminal state 'done' ends with outcome '{outcome}', which isn't a recipe outcome at r.yaml:1:1 (states.done.outcome); use resolved, unresolved, alreadySatisfied or failed"],
            CheckWiring(recipe).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("resolved")]
    [InlineData("unresolved")]
    [InlineData("alreadySatisfied")]
    [InlineData("failed")]
    public void The_closed_set_outcomes_raise_nothing(string outcome) =>
        Assert.Empty(CheckWiring(Of("produce", [("produce", Active("step.produce")), ("done", Terminal(outcome))], [Go("produce", "done", when: Outcome("done"))])));
}
