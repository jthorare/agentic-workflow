using System.Text.Json;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.RecipeReaderTests;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>A state's 'agent:' key (#1269): a bare agent id or '{ differentFrom: draft }'; it is hashed only when set, and a model is never named there.</summary>
public sealed class StateAgentReaderTests
{
    private static string WithAgent(string agent) => Minimal.Replace("    step: step.produce\n", "    step: step.produce\n    agent: " + agent + "\n", StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_bare_agent_id_is_the_agent_the_state_uses() =>
        Assert.Equal(new StateAgent(Use: "codex"), Bind(WithAgent("codex")).States["produce"].Agent);

    [Fact, Trait("Lane", "unit")]
    public void A_differentFrom_names_the_state_to_differ_from() =>
        Assert.Equal(new StateAgent(DifferentFrom: "draft"), Bind(WithAgent("{ differentFrom: draft }")).States["produce"].Agent);

    [Fact, Trait("Lane", "unit")]
    public void A_model_under_agent_is_refused() => Assert.NotEmpty(Lines(WithAgent("{ use: codex, model: gpt-5 }")));

    [Fact, Trait("Lane", "unit")]
    public void A_state_with_no_agent_leaves_it_out_of_the_hashed_form()
    {
        var recipe = Bind(Minimal);

        Assert.Null(recipe.States["produce"].Agent);
        Assert.DoesNotContain("\"agent\"", JsonSerializer.Serialize(recipe.States["produce"], SliceHarness.Json), StringComparison.Ordinal);
    }
}
