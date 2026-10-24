using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.ShowRecipe;

/// <summary>The API's recipe view is a pure function of the recipe (API spec §3.8, E-22), and agrees with the recipe goldens the instance goldens are checked against.</summary>
public sealed class RecipeViewsTests
{
    private static RecipeDocument Read(string path) => Workflow.RecipeComposition.ReadClean(ShippedRecipes.Read(path), path);

    [Fact, Trait("Lane", "unit")]
    public void Every_shipped_recipe_view_equals_its_workflow_recipe_golden_shape()
    {
        var compared = 0;
        foreach (var path in ShippedRecipes.Recipes)
        {
            var document = Read(path);
            var golden = Golden.SourcePath(typeof(RecipeViewsTests).Assembly, $"Golden/Api/workflow-recipe/{document.Recipe.Id.Value}.json");
            if (!File.Exists(golden))
            {
                continue;
            }

            var view = RecipeViews.Of(document.Recipe, new RecipeVersion(document.Recipe.Id, document.Recipe.Version, document.Hash));
            var expected = System.Text.Json.JsonDocument.Parse(File.ReadAllText(golden)).RootElement;
            Assert.Equal(expected.GetProperty("id").GetString(), view.Id.Value);
            Assert.Equal(expected.GetProperty("version").GetInt32(), view.Version);
            Assert.Equal(expected.GetProperty("urgency").GetString(), view.Urgency.ToString().ToLowerInvariant());
            Assert.Equal(Rows(expected, "inputs", static i => $"{i.GetProperty("key")}|{i.GetProperty("kind")}|{i.GetProperty("required")}"),
                [.. view.Inputs.Select(static i => $"{i.Key}|{i.Kind}|{(i.Required ? "True" : "False")}")]);
            Assert.Equal(Rows(expected, "outputs", static o => $"{o.GetProperty("key")}|{o.GetProperty("from")}"), [.. view.Outputs.Select(static o => $"{o.Key}|{o.From}")]);
            Assert.Equal(Rows(expected, "states", static s => $"{s.GetProperty("id")}|{(s.GetProperty("order").ValueKind == System.Text.Json.JsonValueKind.Null ? "-" : s.GetProperty("order").ToString())}"),
                [.. view.States.Select(static s => $"{s.State}|{(s.Order is { } order ? order.ToString(CultureInfo.InvariantCulture) : "-")}")]);
            Assert.Equal(Rows(expected, "loops", static l => $"{l.GetProperty("from")}|{l.GetProperty("to")}|{l.GetProperty("cap")}").Order(StringComparer.Ordinal),   // the golden lists loops in the author's order, the view in the walk's
                view.Loops.Select(static l => string.Create(CultureInfo.InvariantCulture, $"{l.From}|{l.To}|{l.Cap}")).Order(StringComparer.Ordinal));
            compared++;
        }

        Assert.True(compared >= 3, "the dev recipes and workflow.step have goldens to compare");
    }

    private static List<string> Rows(System.Text.Json.JsonElement golden, string name, Func<System.Text.Json.JsonElement, string> row) => [.. golden.GetProperty(name).EnumerateArray().Select(row)];

    [Fact, Trait("Lane", "unit")]
    public void Recipe_with_no_outputs_serves_an_empty_list_and_every_input_names_its_kind()
    {
        var document = Read("sindri/workflow.step.yaml");

        var view = RecipeViews.Of(document.Recipe, new RecipeVersion(document.Recipe.Id, document.Recipe.Version, document.Hash));

        Assert.Empty(view.Outputs);
        Assert.NotEmpty(view.Inputs);
        Assert.All(view.Inputs, static i => Assert.False(string.IsNullOrEmpty(i.Kind)));
        Assert.Contains(view.Loops, static l => l.Name == "revisions" && l.Cap == 5);
    }

    private static RecipeView ViewOf(string inputs)
    {
        var recipe = Workflow.RecipeReaderTests.Bind(Workflow.RecipeReaderTests.Minimal + inputs);
        return RecipeViews.Of(recipe, new RecipeVersion(recipe.Id, recipe.Version, RecipeHash.From("sha256:" + new string('a', 64))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_kind_only_input_serves_the_type_its_kind_pairs_with()
    {
        var view = ViewOf("inputs: [{ key: fact:doc, kind: file }, { key: fact:hook, kind: webhook }, { key: fact:plain }, { key: fact:typed, type: link, kind: file }]\n");

        Assert.Equal([FactType.File, FactType.Payload, FactType.Text, FactType.Link], [.. view.Inputs.Select(static i => i.Type)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_view_type_of_each_input_is_the_type_an_instance_fact_row_serves()
    {
        var recipe = Workflow.RecipeReaderTests.Bind(Workflow.RecipeReaderTests.Minimal + "inputs: [{ key: fact:doc, kind: file }, { key: fact:hook, kind: webhook }, { key: fact:plain }]\n");

        var view = RecipeViews.Of(recipe, new RecipeVersion(recipe.Id, recipe.Version, RecipeHash.From("sha256:" + new string('a', 64))));

        var interpreted = GetInstance.StatusCases.Recipe with { Recipe = recipe };
        var rows = FactRows.Of(EngineFixture.At(EngineFixture.Step, "produce"), interpreted, task: null, new Dictionary<FactKey, FactProvenance>());

        Assert.Equal([.. rows.Where(static r => r.Direction == FactDirection.Input).Select(static r => r.Type)], [.. view.Inputs.Select(static i => i.Type)]);
    }
}
