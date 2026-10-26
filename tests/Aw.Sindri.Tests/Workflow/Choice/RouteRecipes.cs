using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow.Choice;

/// <summary>The decision-tree recipes the choice tests read: the dev evidence route, and the invalid fixtures' compact route (two choices and a desk twin) with one fault edited in.</summary>
internal static class RouteRecipes
{
    public const string Dev = "dev/workflow.dev-evidence-route.yaml";

    /// <summary>The compact route of the invalid fixtures with no fault: RCP-CHOICE-005's fixture with its uncertain transition sent to the desk twin.</summary>
    public static string Compact { get; } = Edit(Fixture("RCP-CHOICE-005"), "from: route, to: prove, on: step.completed, when: { outcome: uncertain }", "from: route, to: route-desk, on: step.completed, when: { outcome: uncertain }");

    /// <summary>The route state's Step, from the shipped dev route.</summary>
    public static StepUse DevStep(string state) => EvalComposition.Recipe(Dev).States[state].Step!;

    public static string Fixture(string name) => RecipeFixtures.Read($"invalid/{name}.yaml");

    /// <summary><paramref name="yaml"/> with <paramref name="find"/> replaced; fails when it isn't there, so a test can't pass by editing nothing.</summary>
    public static string Edit(string yaml, string find, string replace)
    {
        Assert.Contains(find, yaml, StringComparison.Ordinal);
        return yaml.Replace(find, replace, StringComparison.Ordinal);
    }

    /// <summary>The reader's findings and the validator's errors against the shipped catalog, as lines.</summary>
    public static IReadOnlyList<string> Lines(string yaml, string file = "route.yaml", BlockCatalog? catalog = null)
    {
        var reading = RecipeComposition.Read(yaml, file);
        var document = Assert.IsType<RecipeDocument>(reading.Document);
        var errors = RecipeValidator.Validate(document, new ValidationContext(catalog ?? FixtureCatalog.Shipped(), RecipeProfileFacts.None, InMemoryChildRecipes.None, BaseRecipe: null))
            .Where(f => f.Severity == FindingSeverity.Error);
        return [.. reading.Findings.Concat(errors).Select(f => f.ToLine())];
    }
}
