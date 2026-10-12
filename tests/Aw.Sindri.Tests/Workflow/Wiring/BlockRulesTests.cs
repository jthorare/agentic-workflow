using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>RCP-BLOCK-001–003 (blocks registered, of the right kind, with fitting parameters), RCP-EVID-001 and RCP-EVENT-001, spec §2, §3.4, §3.5, W5.</summary>
public sealed class BlockRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_unknown_Step_is_reported()
    {
        // The recipe of invalid/RCP-BLOCK-001.yaml.
        var recipe = Of("produce", [("produce", Active("step.prodcue")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"))], id: "workflow.block-unknown");

        Assert.Equal(
            ["RCP-BLOCK-001: block 'step.prodcue' isn't registered at RCP-BLOCK-001.yaml:8:5 (states.produce.step.use); name a registered step block, or fix the id"],
            CheckBlocks(recipe, new Located("RCP-BLOCK-001.yaml", [("states.produce.step", 8, 5)])).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void Unknown_inputs_gates_and_effects_are_reported()
    {
        var recipe = Of("produce", [("produce", Active("step.produce", inputs: [Use("input.context.search")])), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done")) with { Gates = [Gate("gate.route.ready")], Effects = [Use("effect.notify.chat")] }]);

        Assert.Equal(
            [
                "RCP-BLOCK-001: block 'input.context.search' isn't registered at r.yaml:1:1 (states.produce.step.inputs[0].use); name a registered input block, or fix the id",
                "RCP-BLOCK-001: block 'gate.route.ready' isn't registered at r.yaml:1:1 (transitions[0].gates[0].use); name a registered gate block, or fix the id",
                "RCP-BLOCK-001: block 'effect.notify.chat' isn't registered at r.yaml:1:1 (transitions[0].effects[0].use); name a registered effect block, or fix the id",
            ],
            CheckBlocks(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_block_used_where_another_kind_belongs_is_reported()
    {
        // The recipe of invalid/RCP-BLOCK-002.yaml: a Step named as a gate.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done")) with { Gates = [Gate("step.verify")] }], id: "workflow.block-kind");

        Assert.Equal(
            ["RCP-BLOCK-002: 'step.verify' is a step block, but a transition's gates take gate blocks at RCP-BLOCK-002.yaml:15:85 (transitions[0].gates[0].use); name a gate block here (gate.<area>.<name>)"],
            CheckBlocks(recipe, new Located("RCP-BLOCK-002.yaml", [("transitions[0].gates[0]", 15, 85)])).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_place_takes_its_own_kind()
    {
        var recipe = Of("produce", [("produce", Active("gate.budget.available", inputs: [Use("step.checks")])), ("done", Terminal())],
            [Go("produce", "done") with { Effects = [Use("gate.route.startable")] }]);

        Assert.Equal(
            [
                "RCP-BLOCK-002: 'gate.budget.available' is a gate block, but a state's step takes step blocks at r.yaml:1:1 (states.produce.step.use); name a step block here (step.<area>.<name>)",
                "RCP-BLOCK-002: 'step.checks' is a step block, but a Step's inputs take input blocks at r.yaml:1:1 (states.produce.step.inputs[0].use); name an input block here (input.<area>.<name>)",
                "RCP-BLOCK-002: 'gate.route.startable' is a gate block, but a transition's effects take effect blocks at r.yaml:1:1 (transitions[0].effects[0].use); name an effect block here (effect.<area>.<name>)",
            ],
            CheckBlocks(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_parameter_is_reported()
    {
        // The recipe of invalid/RCP-BLOCK-003.yaml.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done")) with { Gates = [Gate("gate.budget.available", with: With(("limit", Literal("5"))))] }],
            id: "workflow.block-parameter");

        Assert.Equal(
            ["RCP-BLOCK-003: block 'gate.budget.available' has no parameter 'limit' at RCP-BLOCK-003.yaml:16:53 (transitions[0].gates[0].with.limit); remove it, since 'gate.budget.available' takes no parameters"],
            CheckBlocks(recipe, new Located("RCP-BLOCK-003.yaml", [("transitions[0].gates[0].with.limit", 16, 53)])).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void Missing_and_mistyped_parameters_are_reported()
    {
        var gate = Gate("gate.review.judge", 0.8, With(
            ("rounds", Literal("\"two\"")), ("strict", Literal("1")), ("tags", Literal("[\"a\", {\"b\": 1}]")), ("colour", Literal("\"red\""))));
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]) with { Gates = [gate] }]);

        Assert.Equal(
            [
                "RCP-BLOCK-003: parameter 'rounds' of 'gate.review.judge' takes a number, not text at r.yaml:1:1 (transitions[0].gates[0].with.rounds); give it a number",
                "RCP-BLOCK-003: parameter 'strict' of 'gate.review.judge' takes a flag (true or false), not a number at r.yaml:1:1 (transitions[0].gates[0].with.strict); give it a flag (true or false)",
                "RCP-BLOCK-003: parameter 'tags' of 'gate.review.judge' takes a list of strings, numbers or flags, not a list with an object or a list in it at r.yaml:1:1 (transitions[0].gates[0].with.tags); give it a list of strings, numbers or flags",
                "RCP-BLOCK-003: block 'gate.review.judge' has no parameter 'colour' at r.yaml:1:1 (transitions[0].gates[0].with.colour); remove it, since 'gate.review.judge' takes question, rounds, strict, tags",
                "RCP-BLOCK-003: block 'gate.review.judge' needs parameter 'question' at r.yaml:1:1 (transitions[0].gates[0].with.question); add with: { question: <text> }",
            ],
            CheckBlocks(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void Fact_and_profile_values_fit_any_parameter_type()
    {
        var gate = Gate("gate.review.judge", 0.8, With(("question", FactValue("fact:output")), ("rounds", new WithValue(Literal: null, Fact: null, "workflow.rounds"))));
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]) with { Gates = [gate] }]);

        Assert.Empty(CheckBlocks(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_needs_a_registered_provider()
    {
        // The recipe of invalid/RCP-EVID-001.yaml.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done")) with { Evidence = [new EvidenceCheck("parity", EvidencePoint.Head, EvidenceExpectation.Pass)] }],
            id: "workflow.evidence");

        Assert.Equal(
            ["RCP-EVID-001: evidence kind 'parity' has no registered provider (evidence.parity) at RCP-EVID-001.yaml:16:20 (transitions[0].evidence[0].kind); name a kind a registered evidence provider declares, or register evidence.parity"],
            CheckBlocks(recipe, new Located("RCP-EVID-001.yaml", [("transitions[0].evidence[0].kind", 16, 20)])).Lines());
        Assert.Empty(CheckBlocks(recipe with
        {
            Transitions = [recipe.Transitions[0] with { Evidence = [new EvidenceCheck("checks", EvidencePoint.Base, EvidenceExpectation.Fail)] }],
        }));
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_a_bad_kind_point_or_expectation_is_reported()
    {
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [Go("produce", "done", when: Outcome("done")) with { Evidence = [new EvidenceCheck("Checks!", (EvidencePoint)7, (EvidenceExpectation)9)] }]);

        Assert.Equal(
            [
                "RCP-EVID-001: evidence kind 'Checks!' has no registered provider (evidence.Checks!) at r.yaml:1:1 (transitions[0].evidence[0].kind); name a kind a registered evidence provider declares, or register evidence.Checks!",
                "RCP-EVID-001: evidence point '7' isn't base or head at r.yaml:1:1 (transitions[0].evidence[0].at); use at: base or at: head",
                "RCP-EVID-001: evidence expectation '9' isn't pass or fail at r.yaml:1:1 (transitions[0].evidence[0].expect); use expect: pass or expect: fail",
            ],
            CheckBlocks(recipe).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unregistered_event_is_reported()
    {
        // The recipe of invalid/RCP-EVENT-001.yaml.
        var recipe = Of("triaged", [("triaged", WaitForEvent("item.rooted")), ("done", Terminal())],
            [Go("triaged", "done", "wait.satisfied", Outcome("startable"))], id: "workflow.event");

        Assert.Equal(
            ["RCP-EVENT-001: event 'item.rooted' isn't registered at RCP-EVENT-001.yaml:8:16 (states.triaged.waitFor.event); wait for a registered event: claim.created, item.routed, notify.delivered"],
            CheckBlocks(recipe, new Located("RCP-EVENT-001.yaml", [("states.triaged.waitFor.event", 8, 16)])).Lines());
    }
}
