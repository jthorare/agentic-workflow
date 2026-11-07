using Aw.Kernel.Testing;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
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

    public static TheoryData<string, string, bool> OutputsByOutcome() => new()
    {
        { "review", "pass", true },
        { "review", "needsChanges", true },
        { "bug-fix-orchestrator", "partial", true },
        { "autoplan", "needsWork", true },
        { "autoplan", "blocked", false },
    };

    // why: 12a: `sindri skill run review --follow` printed no `fact:reviewFindings` for a needsChanges run, because only the success transition carried the outputs; a result the skill finished with keeps them, a blocked one has none
    [Theory]
    [MemberData(nameof(OutputsByOutcome))]
    public void A_finished_outcome_s_transition_produces_the_block_s_outputs_except_a_blocked_one(string block, string outcome, bool produces)
    {
        var manifest = new[] { SkillFixtures.Review, SkillFixtures.Autoplan, SkillFixtures.BugFixOrchestrator }.Single(m => string.Equals(m.Name, block, StringComparison.Ordinal));
        var loaded = new LoadedSkillBlock(manifest.Name, manifest, SkillDescriptors.Build(manifest).Value, "block.yaml", SkillDescriptors.HashOf(manifest));
        var plan = SkillTrialRecipe.Build(loaded, [.. manifest.Inputs.Where(static i => i.Required).Select(static i => i.Key)]).Value;

        var recipe = Assert.IsType<RecipeDocument>(RecipeComposition.Read(plan.Text, plan.Id.Value + ".yaml").Document).Recipe;
        var transition = recipe.Transitions.Single(t => t.When?.Outcome?.Value == outcome);

        Assert.Equal(produces ? [.. loaded.Descriptor.Outputs.Select(static o => o.Fact.Value)] : [], [.. transition.Produces.Select(static p => p.Fact.Value)]);
    }

    [Fact]
    public void A_first_trial_registers_at_version_1() =>
        Assert.Equal(1, SkillTrialRecipe.VersionFor(Versions(), HashAt(foldered: false)));

    [Fact]
    public void A_trial_whose_text_is_registered_reuses_that_version()
    {
        var hashAt = HashAt(foldered: false);

        Assert.Equal(5, SkillTrialRecipe.VersionFor(Versions(Registered(3, HashAt(foldered: true)(3)), Registered(5, hashAt(5)), Registered(9, HashAt(foldered: true)(9))), hashAt));
    }

    // why: the version came from a hash of the manifest, so an edited block.yaml or a changed builder could land below the highest registered: RCP-VERSION-002 in F-1's hand test
    [Fact]
    public void A_changed_trial_registers_one_above_the_highest_registered_version()
    {
        var other = HashAt(foldered: true);

        Assert.Equal(2006301274, SkillTrialRecipe.VersionFor(Versions(Registered(7, other(7)), Registered(2006301273, other(2006301273))), HashAt(foldered: false)));
    }

    private static Func<int, RecipeHash?> HashAt(bool foldered)
    {
        var loaded = _blocks.Single(static b => string.Equals(b.Manifest.Name, "review", StringComparison.Ordinal));
        return version => Assert.IsType<RecipeDocument>(RecipeComposition.Read(SkillTrialRecipe.Build(loaded, ["pr"], foldered, version).Value.Text, "trial.yaml").Document).Hash;
    }

    private static Dictionary<int, RecipeHash> Versions(params KeyValuePair<int, RecipeHash>[] registered) => new(registered);

    private static KeyValuePair<int, RecipeHash> Registered(int version, RecipeHash? hash) => new(version, Assert.NotNull(hash));

    [Theory]
    [InlineData("ended-pass", "pass")]
    [InlineData("ended-needs-changes", "needsChanges")]
    [InlineData("failed", null)]
    [InlineData("run", null)]
    public void The_block_outcome_comes_back_from_the_terminal_state(string state, string? outcome) =>
        Assert.Equal(outcome, SkillTrialRecipe.OutcomeOf(state));

    [Fact]
    public void A_trial_above_the_last_version_is_refused()
    {
        var registered = Versions(Registered(int.MaxValue, HashAt(foldered: true)(int.MaxValue)));

        _ = Assert.Throws<OverflowException>(() => SkillTrialRecipe.VersionFor(registered, HashAt(foldered: false)));
    }
}
