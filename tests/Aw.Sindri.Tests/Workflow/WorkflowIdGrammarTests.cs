using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow;

public sealed class WorkflowIdGrammarTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.item", true)]
    [InlineData("workflow.dev-timeout", true)]
    [InlineData("workflow.", false)]
    [InlineData("workflow.Item", false)]
    [InlineData("item", false)]
    [InlineData("workflow.item.step", false)]
    [InlineData("workflow.-x", false)]
    public void A_recipe_id_is_workflow_dot_a_kebab_name(string text, bool valid) =>
        Assert.Equal(valid, RecipeId.TryFrom(text, out _));

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_id_over_64_characters_is_refused_and_its_name_drops_the_prefix()
    {
        Assert.False(RecipeId.TryFrom("workflow." + new string('a', 56), out _));
        Assert.Equal("step", RecipeId.From("workflow.step").Name);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("gate.budget.available", true)]
    [InlineData("input.context.retrieve", true)]
    [InlineData("effect.notify.desk", true)]
    [InlineData("evidence.checks.ran", true)]
    [InlineData("step.produce", true)]
    [InlineData("step.bugfix.reproduce", true)]
    [InlineData("step", false)]
    [InlineData("judge.x.y", false)]
    [InlineData("gate.a.b.c", false)]
    [InlineData("gate.Budget.ok", false)]
    public void A_block_id_is_a_kind_prefix_an_optional_area_and_a_name(string text, bool valid) =>
        Assert.Equal(valid, BlockId.TryFrom(text, out _));

    [Theory, Trait("Lane", "unit")]
    [InlineData("gate.route.startable", BlockKind.Gate)]
    [InlineData("evidence.checks.ran", BlockKind.EvidenceProvider)]
    [InlineData("step.produce", BlockKind.Step)]
    [InlineData("effect.claim.ref", BlockKind.Effect)]
    [InlineData("input.context.retrieve", BlockKind.StepInput)]
    public void A_block_ids_prefix_names_its_kind(string text, BlockKind kind) =>
        Assert.Equal(kind, BlockKinds.Of(BlockId.From(text)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("fact:item", true)]
    [InlineData("fact:stepSpec", true)]
    [InlineData("evidence:checks@head", true)]
    [InlineData("evidence:ui-evidence@base", true)]
    [InlineData("fact:", false)]
    [InlineData("fact:Step", false)]
    [InlineData("evidence:checks", false)]
    [InlineData("evidence:checks@tip", false)]
    [InlineData("item", false)]
    public void A_fact_key_is_fact_or_evidence_at_base_or_head(string text, bool valid) =>
        Assert.Equal(valid, FactKey.TryFrom(text, out _));

    [Fact, Trait("Lane", "unit")]
    public void An_evidence_key_says_so() =>
        Assert.True(FactKey.From("evidence:checks@head").IsEvidence && !FactKey.Answer.IsEvidence);

    [Theory, Trait("Lane", "unit")]
    [InlineData("item.routed", true)]
    [InlineData("claim.created", true)]
    [InlineData("notify.delivered", true)]
    [InlineData("item", false)]
    [InlineData("item.routed.now", false)]
    [InlineData("Item.routed", false)]
    public void An_event_kind_is_area_dot_name(string text, bool valid) =>
        Assert.Equal(valid, EventKind.TryFrom(text, out _));

    [Theory, Trait("Lane", "unit")]
    [InlineData("resolved", true)]
    [InlineData("alreadySatisfied", true)]
    [InlineData("notStartable", true)]
    [InlineData("Resolved", false)]
    [InlineData("not-startable", false)]
    [InlineData("", false)]
    public void An_outcome_is_a_camel_case_word(string text, bool valid) =>
        Assert.Equal(valid, OutcomeName.TryFrom(text, out _));
}
