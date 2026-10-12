using System.Text.RegularExpressions;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The validate-only fixtures (spec §12): 'workflow.bugfix' (modes A and C) and 'workflow.direction', against the test catalog.</summary>
public sealed partial class ValidRecipeFixturesTests
{
    public static TheoryData<string> Fixtures => ["valid/workflow.bugfix.yaml", "valid/workflow.direction.yaml"];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Fixtures))]
    public void Valid_fixture_reads_without_findings(string fixture) =>
        _ = RecipeComposition.ReadClean(RecipeFixtures.Read(fixture), Path.GetFileName(fixture));

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Fixtures))]
    public void Valid_fixture_validates_against_the_test_catalog(string fixture)
    {
        var document = RecipeComposition.ReadClean(RecipeFixtures.Read(fixture), Path.GetFileName(fixture));

        Assert.Empty(RecipeComposition.Errors(document, FixtureCatalog.WithFixtures(), InMemoryChildRecipes.None));
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Fixtures))]
    public void Valid_fixture_holds_no_approval_data(string fixture) =>
        Assert.False(ApprovalData.In(RecipeFixtures.Read(fixture)), fixture);

    [Fact, Trait("Lane", "unit")]
    public void Bugfix_fixture_names_only_bugfix_steps()
    {
        var uses = StepUse.Matches(RecipeFixtures.Read("valid/workflow.bugfix.yaml"));

        Assert.NotEmpty(uses);
        Assert.All(uses, m => Assert.StartsWith("step.bugfix.", m.Groups["id"].Value, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"step:\s*(?<id>step\.[a-z.-]+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StepUse { get; }
}
