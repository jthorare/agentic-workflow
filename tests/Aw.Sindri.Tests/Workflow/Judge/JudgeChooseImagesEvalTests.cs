using System.Text.Json;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Choice;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow.Judge;

/// <summary>
/// The eval case for the images parameter on a route state (images spec 5.1, "an eval case on a fixture recipe"): the recipe is the compact
/// route of the invalid fixtures with an images fact the instance starts with, and the case has the judge decide with two frames.
/// </summary>
public sealed class JudgeChooseImagesEvalTests
{
    private const string Recipe = "dev/workflow.choice-images.yaml";
    private const string Case = "dev/evals/workflow.choice-images/happy.jsonl";

    private static readonly string[] _frames = ["/work/ui-evidence/home.png", "/work/ui-evidence/home-base.png"];

    [Fact, Trait("Lane", "unit")]
    public async Task A_route_whose_judge_decides_with_two_frames_reaches_its_branch()
    {
        var yaml = RouteRecipes.Edit(RouteRecipes.Compact, "        facts: [fact:change]\n", "        facts: [fact:change]\n        images: \"fact:frames\"\n")
            .Replace("id: workflow.choice-twin", "id: workflow.choice-images", StringComparison.Ordinal)
            .Replace("inputs: [{ key: fact:change, label: \"Change\", type: text, kind: text }]", "inputs: [{ key: fact:change, label: \"Change\", type: text, kind: text }, { key: fact:frames, label: \"Frames\", type: text, kind: text }]", StringComparison.Ordinal)
            .Replace("displayName: Test recipe", "evals: [evals/workflow.choice-images/happy.jsonl]\ndisplayName: Test recipe", StringComparison.Ordinal);
        var frames = JsonSerializer.Serialize(JsonSerializer.Serialize(_frames));
        var files = new InMemoryRecipeFiles()
            .With(Recipe, yaml)
            .With(Case, string.Join('\n',
            [
                """{"start":{"inputs":{"fact:change":"Rename the export button.","fact:frames":""" + frames + "}}}",
                """{"trigger":{"kind":"step.completed","outcome":"ui"},"expect":{"state":"prove"}}""",
                """{"trigger":{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"proof for ui"}},"expect":{"state":"done","outcome":"resolved"}}""",
                string.Empty,
            ]));

        var response = (await EvalComposition.Service(files).Handle(new EvalRecipeRequest(Recipe, EvalMode.Run), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["happy pass"], response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
        Assert.True(response.Passed);
    }
}
