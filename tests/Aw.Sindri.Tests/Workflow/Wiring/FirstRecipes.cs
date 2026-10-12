using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>The long forms of §12's workflow.step and workflow.item, as the reader binds recipes/sindri/*.yaml.</summary>
internal static class FirstRecipes
{
    public static Recipe Step { get; } = Of(
        "produce",
        [
            ("produce", Active("step.produce", loop: "revisions")),
            ("checks", Active("step.checks")),
            ("verify", Active("step.verify")),
            ("done", Terminal()),
        ],
        [
            Go("produce", "checks", when: Outcome("done"), produces: ["fact:output"]),
            Go("checks", "verify", when: Outcome("passed")),
            Go("checks", "produce", when: Outcome("failed"), produces: ["fact:findings"]),
            Go("verify", "done", when: Outcome("accepted")),
            Go("verify", "produce", when: Outcome("rejected"), produces: ["fact:findings"]),
        ],
        inputs: ["fact:spec"],
        id: "workflow.step",
        loops: ["revisions"]);

    public static Recipe Item { get; } = Of(
        "triaged",
        [
            ("triaged", WaitForEvent("item.routed")),
            ("startable", WaitForResource(ResourceKind.Slot)),
            ("claimed", WaitForEvent("claim.created")),
            ("running", WaitForChild("workflow.step", ("fact:spec", FactValue("fact:stepSpec")))),
            ("done", Terminal()),
            ("dropped", Terminal("unresolved")),
        ],
        [
            Go("triaged", "startable", "wait.satisfied", Outcome("startable"), ["fact:stepSpec"]) with { Gates = [Gate("gate.route.startable")] },
            Go("triaged", "dropped", "wait.satisfied", Outcome("notStartable")),
            Go("startable", "claimed", "wait.satisfied") with { Gates = [Gate("gate.budget.available")], Effects = [Use("effect.claim.ref")] },
            Go("claimed", "running", "wait.satisfied", Outcome("created")),
            Go("claimed", "dropped", "wait.satisfied", Outcome("claimExists")),
            Go("running", "done", "wait.satisfied", Outcome("resolved")),
            Go("running", "dropped", "wait.satisfied", Outcome("cancelled")),
        ],
        inputs: ["fact:item"],
        id: "workflow.item");
}
