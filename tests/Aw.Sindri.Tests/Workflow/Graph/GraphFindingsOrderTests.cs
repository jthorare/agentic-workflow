using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>The graph rules report every finding they have, ordered by where each node starts in the file (spec §9.1).</summary>
public sealed class GraphFindingsOrderTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_recipe_with_several_faults_reports_every_finding_in_file_order()
    {
        var recipe = Of(
            "produce",
            [
                ("produce", Active(loop: "rounds", next: "Produce it for {ticket}.")),
                ("running", WaitForChild("workflow.faults", TimeoutAction.Retry)),
                ("done", Terminal()),
                ("orphan", Terminal("unresolved")),
            ],
            [Go("produce", "running"), Go("running", "done", "wait.satisfied"), Go("produce", "done", "command.cancel")],
            [("revisions", new LoopSpec(0, []))],
            id: "workflow.faults");

        var lines = Lines(recipe, "faults.yaml",
        [
            ("states.produce.loop", 9, 5),
            ("states.produce.next", 11, 5),
            ("states.running.waitFor.child.recipe", 14, 25),
            ("states.running.timeout.then", 15, 28),
            ("states.orphan", 20, 3),
            ("transitions[2].on", 25, 31),
            ("loops.revisions.cap", 26, 23),
            ("loops.revisions.noProgress", 26, 31),
        ]);

        Assert.Equal(
            [
                "RCP-LOOP-001: state 'produce' names loop 'rounds', which isn't declared at faults.yaml:9:5 (states.produce.loop); declare it under loops, or name a declared loop",
                "RCP-NEXT-001: the next of state 'produce' uses '{ticket}', which isn't a recipe variable at faults.yaml:11:5 (states.produce.next); use only {item}, {instance}, {state}, {user}, {child}, {loop.<name>}, {loop.<name>.cap} and {budget.tokens.left}",
                "RCP-CHILD-002: child recipes form a cycle: workflow.faults -> workflow.faults at faults.yaml:14:25 (states.running.waitFor.child.recipe); remove a child wait so no recipe starts itself, directly or through its children",
                "RCP-TIME-002: state 'running' retries a child wait when it times out at faults.yaml:15:28 (states.running.timeout.then); use then: park or then: escalate, since a child can't be retried",
                "RCP-REACH-001: state 'orphan' is unreachable from 'produce' at faults.yaml:20:3 (states.orphan); add a transition into it or remove it",
                "RCP-TRIG-002: verb 'cancel' is reserved for sindri workflow's own commands at faults.yaml:25:31 (transitions[2].on); choose another verb",
                "RCP-LOOP-001: loop 'revisions' has cap 0 at faults.yaml:26:23 (loops.revisions.cap); set cap to 1 or more",
                "RCP-LOOP-003: loop 'revisions' has an empty noProgress at faults.yaml:26:31 (loops.revisions.noProgress); name at least one of findings, emptyDiff and sameFailure, or leave noProgress out to use all three",
            ],
            lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void Findings_at_the_same_place_keep_rule_order()
    {
        var recipe = Of("produce", [("produce", Active(then: null, next: "{x}")), ("done", Terminal())], [Go("produce", "done")]);

        Assert.Equal(["RCP-NEXT-001", "RCP-TIME-001"], Lines(recipe).Select(l => l[..12]), StringComparer.Ordinal);
    }
}
