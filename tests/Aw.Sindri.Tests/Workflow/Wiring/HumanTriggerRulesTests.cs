using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>RCP-HUMAN-001 (PB spec W23): an effect that acts with a human's authority sits only on a transition a human drives.</summary>
public sealed class HumanTriggerRulesTests
{
    private static readonly Located _at = new("r.yaml", [("transitions[0].effects[0]", 3, 7), ("transitions[1].effects[0]", 4, 7)]);

    private static RecipeTransition Merge(RecipeTransition transition) => transition with { Effects = [Use("effect.test.merge")] };

    private static Recipe Of(params RecipeTransition[] transitions) => WiringRecipe.Of(
        "ready",
        [("ready", Active("step.produce")), ("approve", Active("step.test.approve")), ("hold", WaitForEvent("item.routed")), ("done", Terminal())],
        transitions);

    [Fact, Trait("Lane", "unit")]
    public void A_step_completed_out_of_an_agent_Step_is_refused()
    {
        var recipe = Of(Merge(Go("ready", "done", when: Outcome("done"))));

        var finding = Assert.Single(CheckHumanTrigger(recipe, _at));

        Assert.Equal(("RCP-HUMAN-001", "transitions[0].effects[0].use"), (finding.Rule, finding.Path));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_transition_from_several_states_needs_every_one_human_only()
    {
        var recipe = Of(Merge(Go("approve", "done", when: Outcome("approved")) with { From = ["approve", "ready"] }));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_wait_satisfied_with_no_requirement_is_refused()
    {
        var recipe = Of(Merge(Go("hold", "done", on: "wait.satisfied")));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_fact_nothing_produces_is_refused()
    {
        var recipe = Of(Merge(Go("hold", "done", on: "wait.satisfied", when: Fact("fact:merge", FactPresence.Present))));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_absent_fact_does_not_carry_the_authority()
    {
        var recipe = Of(
            Merge(Go("hold", "done", on: "wait.satisfied", when: Fact("fact:stepSpec", FactPresence.Absent))),
            Go("approve", "hold", when: Outcome("approved"), produces: ["fact:stepSpec"]));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("valid/human-trigger-command.yaml")]
    [InlineData("valid/human-trigger-human-only.yaml")]
    [InlineData("valid/human-trigger-carried.yaml")]
    public void The_valid_forms_pass_the_validator(string fixture)
    {
        var document = RecipeComposition.ReadClean(RecipeFixtures.Read(fixture), Path.GetFileName(fixture));

        Assert.Empty(RecipeComposition.Errors(document, WiringCatalog.WithTestBlocks, InMemoryChildRecipes.None));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_carrying_state_must_be_entered_only_by_a_command_with_the_effect()
    {
        var recipe = WiringRecipe.Of(
            "ready",
            [("ready", Active("step.produce")), ("merging", WaitForEvent("item.routed")), ("tidy", Active("step.produce")), ("done", Terminal())],
            [
                Merge(Go("ready", "merging", on: "command.merge")),
                Go("ready", "merging", when: Outcome("done")),
                Go("merging", "tidy", on: "wait.satisfied", produces: ["fact:stepSpec"]),
                Merge(Go("tidy", "done", when: Fact("fact:stepSpec", FactPresence.Present))),
            ]);

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_fact_that_is_also_a_recipe_input_is_refused()
    {
        var recipe = Carried("fact:stepSpec", inputs: [DefaultInput, "fact:stepSpec"]);

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_fact_a_Step_also_writes_is_refused()
    {
        var recipe = Carried("fact:output");

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_fact_with_a_second_producer_off_an_agent_state_is_refused()
    {
        var recipe = Carried("fact:stepSpec", extra: Go("ready", "tidy", when: Outcome("done"), produces: ["fact:stepSpec"]));

        _ = Assert.Single(CheckHumanTrigger(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_fact_inside_any_or_not_carries_nothing()
    {
        var any = new WhenClause(Outcome: null, Fact: null, Is: null, Loop: null, Below: null, All: null, Any: [Fact("fact:stepSpec", FactPresence.Present)], Not: null);

        _ = Assert.Single(CheckHumanTrigger(Carried("fact:stepSpec", when: any)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_carried_form_passes() => Assert.Empty(CheckHumanTrigger(Carried("fact:stepSpec")));

    /// <summary>The carried shape: a human's merge command enters 'merging', whose leaving transition records the fact the guarded merge needs.</summary>
    private static Recipe Carried(string fact, string[]? inputs = null, RecipeTransition? extra = null, WhenClause? when = null) => WiringRecipe.Of(
        "ready",
        [("ready", Active("step.produce")), ("merging", WaitForEvent("item.routed")), ("tidy", Active("step.produce")), ("done", Terminal())],
        [
            Merge(Go("ready", "merging", on: "command.merge")),
            Go("merging", "tidy", on: "wait.satisfied", produces: [fact]),
            Merge(Go("tidy", "done", when: when ?? Fact(fact, FactPresence.Present))),
            .. extra is null ? Array.Empty<RecipeTransition>() : [extra],
        ],
        inputs);

    private static WhenClause Fact(string key, FactPresence presence) =>
        new(Outcome: null, Core.Workflow.Ids.FactKey.From(key), presence, Loop: null, Below: null, All: null, Any: null, Not: null);
}
