using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>RCP-THRESH-001/002 (W4: thresholds follow the descriptor's rung), RCP-EFFECT-001 (the profile's effect allowlist, S1-D11) and RCP-RES-001 (a resource with no allocator), spec §3.4, §9.1.</summary>
public sealed class GateEffectResourceRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_judge_question_gate_needs_a_threshold()
    {
        // The recipe of invalid/RCP-THRESH-001.yaml; the gate's node is at 17:15 and has no threshold.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [GatedBy(Gate("gate.review.judge", threshold: null, With(("question", Literal("\"Is the output done?\"")))))], id: "workflow.threshold-missing");

        Assert.Equal(
            ["RCP-THRESH-001: gate 'gate.review.judge' is a judge question and has no threshold at RCP-THRESH-001.yaml:17:15 (transitions[0].gates[0].threshold); add threshold: a score above 0 and at most 1, for example 0.8"],
            CheckBlocks(recipe, new Located("RCP-THRESH-001.yaml", [("transitions[0].gates[0]", 17, 15)])).Lines());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_deterministic_gate_takes_no_threshold()
    {
        // The recipe of invalid/RCP-THRESH-002.yaml.
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [GatedBy(Gate("gate.budget.available", 0.5))], id: "workflow.threshold-deterministic");

        Assert.Equal(
            ["RCP-THRESH-002: gate 'gate.budget.available' is deterministic and takes no threshold at RCP-THRESH-002.yaml:16:45 (transitions[0].gates[0].threshold); remove threshold"],
            CheckBlocks(recipe, new Located("RCP-THRESH-002.yaml", [("transitions[0].gates[0].threshold", 16, 45)])).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0.0, "0")]
    [InlineData(1.5, "1.5")]
    [InlineData(-0.25, "-0.25")]
    [InlineData(double.NaN, "NaN")]
    public void A_threshold_outside_the_score_range_is_reported(double threshold, string shown)
    {
        var recipe = Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [GatedBy(Gate("gate.review.judge", threshold, With(("question", Literal("\"Done?\"")))))]);

        Assert.Equal(
            [$"RCP-THRESH-002: threshold {shown} of gate 'gate.review.judge' is out of range at r.yaml:1:1 (transitions[0].gates[0].threshold); use a score above 0 and at most 1"],
            CheckBlocks(recipe).Lines());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0.01)]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void A_threshold_in_range_raises_nothing(double threshold) =>
        Assert.Empty(CheckBlocks(Of("produce", [("produce", Active("step.produce")), ("done", Terminal())],
            [GatedBy(Gate("gate.review.judge", threshold, With(("question", Literal("\"Done?\"")))))])));

    [Fact, Trait("Lane", "unit")]
    public void An_effect_off_the_allowlist_is_an_error()
    {
        // The recipe of invalid/RCP-EFFECT-001.yaml, against a profile that allows only effect.claim.ref.
        var recipe = Of("ask", [("ask", Active("step.produce")), ("done", Terminal())],
            [Go("ask", "done", when: Outcome("done")) with { Effects = [Use("effect.notify.desk")] }], id: "workflow.effect");
        var profile = new RecipeProfileFacts(new HashSet<BlockId> { BlockId.From("effect.claim.ref") });

        var findings = CheckBlocks(recipe, new Located("RCP-EFFECT-001.yaml", [("transitions[0].effects[0]", 16, 83)], Profile: profile));

        Assert.Equal(
            ["RCP-EFFECT-001: effect 'effect.notify.desk' isn't on the profile's effect allowlist at RCP-EFFECT-001.yaml:16:83 (transitions[0].effects[0].use); add it to workflow.effects and approve the profile, or remove it"],
            findings.Lines());
        Assert.Equal(FindingSeverity.Error, Assert.Single(findings).Severity);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_the_profile_key_each_effect_is_skipped()
    {
        var recipe = Of("ask", [("ask", Active("step.produce")), ("done", Terminal())],
            [Go("ask", "done", when: Outcome("done")) with { Effects = [Use("effect.notify.desk")] }]);

        var findings = CheckBlocks(recipe);

        Assert.Equal(
            ["RCP-EFFECT-001: effect 'effect.notify.desk' wasn't checked against an effect allowlist, since the profile has no workflow.effects at r.yaml:1:1 (transitions[0].effects[0].use); add workflow.effects to the profile; until then the effect is refused at run time"],
            findings.Lines());
        Assert.Equal(FindingSeverity.Skipped, Assert.Single(findings).Severity);
        Assert.True(RecipeValidator.Passes(findings));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ResourceKind.Heavy, "heavy", "it parks with SND-WF-010 until the heavy-job lock port lands")]
    [InlineData(ResourceKind.Slot, "slot", "it parks with SND-WF-010 until the session-slot stub lands (P7)")]
    public void A_resource_wait_is_information_while_nothing_allocates_it(ResourceKind resource, string name, string fix)
    {
        // The heavy case is the recipe of invalid/RCP-RES-001.yaml.
        var recipe = Of("queued", [("queued", WaitForResource(resource)), ("done", Terminal())], [Go("queued", "done", "wait.satisfied")], id: "workflow.resource");

        var findings = CheckBlocks(recipe, new Located("RCP-RES-001.yaml", [("states.queued.waitFor.resource", 8, 16)]));

        Assert.Equal([$"RCP-RES-001: resource '{name}' has no allocator in this build at RCP-RES-001.yaml:8:16 (states.queued.waitFor.resource); {fix}"], findings.Lines());
        Assert.Equal(FindingSeverity.Information, Assert.Single(findings).Severity);
    }

    private static RecipeTransition GatedBy(GateUse gate) =>
        Go("produce", "done", when: Outcome("done"), produces: ["fact:output"]) with { Gates = [gate] };
}
