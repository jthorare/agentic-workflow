using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>
/// 'workflow.build' v7 (model-check design §4.1): after 'checks' passes the build runs 'step.model.check' on what the change touches, and routes each result: pass or skipped finish the build, a violation, a
/// vacuous model, a spec error or a model too big go back to 'green' inside the revisions loop (cap 6), and a model check that can't run goes to 'model-desk', whose answers are Check again and Stop.
/// The eval cases are the recipe's own (recipes/sindri/evals/workflow.build/model-*.jsonl).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class BuildModelRecipeTests
{
    private const string Build = "sindri/workflow.build.yaml";

    private static readonly string[] _cases =
    [
        "model-pass", "model-skipped", "model-violation-fixed", "model-sent-back", "model-cant-run-again-then-stop", "model-violation-cap",
    ];

    public static TheoryData<string> Cases => [.. _cases];

    private static Recipe Shipped() => RecipeComposition.ReadClean(ShippedRecipes.Read(Build), "workflow.build.yaml").Recipe;

    [Fact]
    public void The_build_is_version_7_and_has_a_model_state_and_a_model_desk()
    {
        var recipe = Shipped();

        Assert.Equal(7, recipe.Version);
        Assert.True(recipe.States.ContainsKey("model"));
        Assert.True(recipe.States.ContainsKey("model-desk"));
    }

    [Fact]
    public void The_model_state_runs_the_check_on_the_touched_specs_with_a_timeout_past_the_steps_limit()
    {
        var model = Shipped().States["model"];

        Assert.Equal("step.model.check", model.Step!.Use.Value);
        Assert.Equal("touched", model.Step.With["scope"].Literal!.Value.GetString());
        Assert.True(model.Timeout!.After.Span > CommandStepLimits.ModelCheckMaxDuration);   // RCP-TIME-003: the state outlasts the Step's 15 min
        Assert.Equal(TimeoutAction.Park, model.Timeout.Then);
        Assert.Equal("Check the design model", model.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(model.Why));
        Assert.False(string.IsNullOrWhiteSpace(model.Next));
    }

    [Fact]
    public void The_model_desk_asks_for_14_days_and_wires_Check_again_and_Stop_only()
    {
        var desk = Shipped().States["model-desk"];

        Assert.Equal("step.model.desk", desk.Step!.Use.Value);
        Assert.Equal((TimeSpan.FromDays(14), TimeoutAction.Park), (desk.Timeout!.After.Span, desk.Timeout.Then));
        Assert.Equal("The model check can't run", desk.DisplayName);
        Assert.Equal(["again", "stop"], desk.OnOutcome!.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);   // 'skip' is scoping's
        Assert.All(desk.OnOutcome.Values, static words => Assert.False(string.IsNullOrWhiteSpace(words)));
    }

    [Fact]
    public void The_build_hands_on_what_the_model_check_found()
    {
        var outputs = Shipped().Outputs;

        Assert.Contains(outputs, static o => o.Key.Value == "fact:modelCheck" && o.From == "model");   // the PR body line reads it
        Assert.Contains(outputs, static o => o.Key.Value == "fact:built" && o.From == "checks");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Each_path_through_the_model_check_ends_where_the_design_says(string name)
    {
        var files = new InMemoryRecipeFiles();
        foreach (var path in ShippedRecipes.Paths)
        {
            _ = files.With(path, ShippedRecipes.Read(path));
        }

        var response = (await EvalComposition.Service(files).Handle(new Aw.Sindri.Features.EvalRecipe.EvalRecipeRequest(Build, Aw.Sindri.Features.EvalRecipe.EvalMode.Run), TestContext.Current.CancellationToken)).Value;

        Assert.Equal($"{name} pass", response.Cases.Single(c => c.Case == name).ToLine());
    }
}
