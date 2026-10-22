using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The recipes under recipes/ (spec §12): the real ones, the dev ones, and their evals (§9.2).</summary>
public sealed partial class ShippedRecipesTests
{
    public static TheoryData<string> Recipes => [.. ShippedRecipes.Recipes];

    public static TheoryData<string> Evals => [.. ShippedRecipes.Evals];

    [Fact, Trait("Lane", "unit")]
    public void The_real_and_dev_recipes_are_shipped() =>
        Assert.Equal(
            [
                "dev/desk-round-trip.yaml", "dev/incident-hold.yaml", "dev/triage-three-state.yaml", "dev/workflow.dev-child-outputs.yaml", "dev/workflow.dev-child-summary.yaml",
                "dev/workflow.dev-evidence-route.yaml",
                "dev/workflow.dev-notify.yaml", "dev/workflow.dev-timeout.yaml",
                "sindri/workflow.build.yaml", "sindri/workflow.design.yaml", "sindri/workflow.docs-sync.yaml", "sindri/workflow.evidence-all.yaml",
                "sindri/workflow.evidence-api.yaml", "sindri/workflow.evidence-db.yaml", "sindri/workflow.evidence-other.yaml", "sindri/workflow.evidence-ui.yaml",
                "sindri/workflow.evidence.yaml", "sindri/workflow.hand-test.yaml", "sindri/workflow.idea-to-brief.yaml", "sindri/workflow.implement.yaml",
                "sindri/workflow.investigate.yaml", "sindri/workflow.item.yaml", "sindri/workflow.line.yaml", "sindri/workflow.phase.yaml", "sindri/workflow.plan.yaml",
                "sindri/workflow.pr-babysitter.yaml", "sindri/workflow.prove-recheck.yaml", "sindri/workflow.prove.yaml", "sindri/workflow.report.yaml", "sindri/workflow.reproduce.yaml",
                "sindri/workflow.review-loop.yaml", "sindri/workflow.scoping.yaml", "sindri/workflow.ship.yaml", "sindri/workflow.step.yaml",
                "sindri/workflow.verify.yaml",
            ],
            ShippedRecipes.Recipes,
            StringComparer.Ordinal);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_reads_without_findings(string path) =>
        _ = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_validates_against_the_shipped_catalog(string path)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));

        Assert.Empty(RecipeComposition.Errors(document, FixtureCatalog.Shipped(), Siblings(path)));
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_lists_every_eval_of_its_own_and_each_one_is_shipped(string path)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));
        var directory = Path.GetDirectoryName(path);
        var own = ShippedRecipes.Evals.Where(e => e.StartsWith($"{directory}/evals/{document.Recipe.Id.Value}/", StringComparison.Ordinal));

        Assert.Equal(own, document.Recipe.Evals.Select(e => $"{directory}/{e}").Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_holds_no_approval_data(string path) =>
        Assert.False(ApprovalData.In(ShippedRecipes.Read(path)), path);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_has_a_happy_eval(string path)
    {
        var id = RecipeIdLine.Match(ShippedRecipes.Read(path)).Groups["id"].Value;

        var directory = $"{Path.GetDirectoryName(path)}/evals/{id}/";

        Assert.Contains(ShippedRecipes.Evals, e => e.StartsWith(directory, StringComparison.Ordinal) && (e.EndsWith("/happy.jsonl", StringComparison.Ordinal) || e.EndsWith("-happy.jsonl", StringComparison.Ordinal)));   // a station with several inputs has one happy path per input (brief-happy, tracker-happy …)
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Evals))]
    public void Eval_file_follows_the_eval_format(string path)
    {
        var lines = ShippedRecipes.Read(path).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length >= 2, $"{path}: a start line and at least one trigger line");
        EvalFormat.AssertStart(lines[0]);
        Assert.All(lines.Skip(1), EvalFormat.AssertStep);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Every_shipped_child_wait_resolves_its_pinned_child(string path)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));
        var siblings = Siblings(path);
        var named = document.Recipe.States.Values.Select(static s => s.WaitFor?.Child?.Recipe).OfType<RecipeId>().Distinct().ToList();
        var found = named.ToDictionary(static id => id, siblings.Resolve);
        Assert.All(found, static f => Assert.NotNull(f.Value));
        var pinned = new PinnedChildRecipes(found.ToDictionary(static f => f.Key, static f => f.Value!), found.ToDictionary(static f => f.Key, static f => f.Value!.Hash));   // why: the Assert.All above holds each

        Assert.All(named, id => Assert.Equal(found[id]!.Hash, pinned.Resolve(id)?.Hash));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_shipped_child_waits_are_the_ones_the_engine_starts_children_for()
    {
        var pairs = ShippedRecipes.Recipes
            .SelectMany(path => RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path)).Recipe.States.Values
                .Select(static s => s.WaitFor?.Child?.Recipe).OfType<RecipeId>().Select(child => (Path.GetFileNameWithoutExtension(path), child.Value)))
            .Order()
            .ToList();

        Assert.Equal(
            [
                ("workflow.dev-child-outputs", "workflow.dev-child-summary"),
                ("workflow.evidence", "workflow.evidence-all"), ("workflow.evidence", "workflow.evidence-api"), ("workflow.evidence", "workflow.evidence-db"),
                ("workflow.evidence", "workflow.evidence-other"), ("workflow.evidence", "workflow.evidence-ui"),
                ("workflow.evidence-all", "workflow.evidence-api"), ("workflow.evidence-all", "workflow.evidence-db"), ("workflow.evidence-all", "workflow.evidence-ui"),
                ("workflow.item", "workflow.step"),
                ("workflow.line", "workflow.design"), ("workflow.line", "workflow.design"),
                ("workflow.line", "workflow.plan"), ("workflow.line", "workflow.scoping"),
                ("workflow.phase", "workflow.build"), ("workflow.phase", "workflow.pr-babysitter"), ("workflow.phase", "workflow.prove"),
                ("workflow.phase", "workflow.review-loop"), ("workflow.phase", "workflow.ship"),
                ("workflow.pr-babysitter", "workflow.docs-sync"), ("workflow.pr-babysitter", "workflow.prove-recheck"),
                ("workflow.prove", "workflow.evidence"), ("workflow.prove", "workflow.hand-test"), ("workflow.prove", "workflow.verify"),
                ("workflow.prove-recheck", "workflow.verify"),
                ("workflow.scoping", "workflow.idea-to-brief"),
            ],
            pairs);
    }

    private static InMemoryChildRecipes Siblings(string path)
    {
        var children = InMemoryChildRecipes.None;
        foreach (var sibling in ShippedRecipes.Recipes.Where(r => Path.GetDirectoryName(r) == Path.GetDirectoryName(path)))
        {
            var reading = RecipeComposition.Read(ShippedRecipes.Read(sibling), Path.GetFileName(sibling));
            if (reading.Document is { } document)
            {
                _ = children.With(new ChildRecipe(document.Recipe, document.Hash));
            }
        }

        return children;
    }

    [GeneratedRegex(@"^id:\s*(?<id>\S+)\s*$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RecipeIdLine { get; }
}
