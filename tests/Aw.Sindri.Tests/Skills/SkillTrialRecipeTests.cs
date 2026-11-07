using Aw.Kernel.Testing;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;
using Aw.Sindri.Tests.Workflow.Choice;

namespace Aw.Sindri.Tests.Skills;

/// <summary>The trial recipe 'sindri skill run' registers (skill flows spec §7) passes the validator the server runs on it, and the CLI reads the block's outcome back from its terminal state.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillTrialRecipeTests
{
    private static readonly IReadOnlyList<LoadedSkillBlock> _blocks = SkillManifestLoader.Load(SkillFixtures.Folder).Loaded;

    private static BlockCatalog Catalog()
    {
        var shipped = FixtureCatalog.Shipped();
        return new BlockCatalog([.. shipped.Blocks, .. _blocks.Select(static b => b.Descriptor)], shipped.Events);
    }

    public static TheoryData<string, bool> Runs() => new()
    {
        { "review", false }, { "review", true }, { "office-hours", false }, { "office-hours-revise", true },
    };

    [Theory]
    [MemberData(nameof(Runs))]
    public void Every_fixture_block_s_trial_recipe_validates(string block, bool foldered)
    {
        var loaded = _blocks.Single(b => string.Equals(b.Manifest.Name, block, StringComparison.Ordinal));
        var plan = SkillTrialRecipe.Build(loaded, [.. loaded.Manifest.Inputs.Select(static i => i.Key)], foldered).Value;

        Assert.Empty(RouteRecipes.Lines(plan.Text, plan.Id.Value + ".yaml", Catalog()));
    }

    // why: the instance's fact rows list only declared outputs, so a trial that declared none showed no 'fact:' line after its run (F-1's hand test, journey 2)
    [Fact]
    public void The_trial_recipe_declares_the_block_s_outputs_under_its_run_state()
    {
        var loaded = _blocks.Single(static b => string.Equals(b.Manifest.Name, "review", StringComparison.Ordinal));
        var plan = SkillTrialRecipe.Build(loaded, [.. loaded.Manifest.Inputs.Select(static i => i.Key)]).Value;

        var recipe = Assert.IsType<RecipeDocument>(RecipeComposition.Read(plan.Text, plan.Id.Value + ".yaml").Document).Recipe;

        Assert.Equal([.. loaded.Descriptor.Outputs.Select(static o => (o.Fact.Value, "run"))], [.. recipe.Outputs.Select(static o => (o.Key.Value, o.From))]);
    }

    [Theory]
    [InlineData("ended-pass", "pass")]
    [InlineData("ended-needs-changes", "needsChanges")]
    [InlineData("failed", null)]
    [InlineData("run", null)]
    public void The_block_outcome_comes_back_from_the_terminal_state(string state, string? outcome) =>
        Assert.Equal(outcome, SkillTrialRecipe.OutcomeOf(state));
}
