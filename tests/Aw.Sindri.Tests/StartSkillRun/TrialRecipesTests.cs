using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryWrites;
using Aw.Sindri.Features.StartSkillRun;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Tests.ApproveRecipe;
using Aw.Sindri.Tests.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.StartSkillRun;

/// <summary>A trial recipe registers one version above the highest, and a run that loses the race for that version, or can't go higher, says so (#2148).</summary>
[Lane(TestLane.Unit)]
public sealed class TrialRecipesTests
{
    private static readonly LoadedSkillBlock _review = SkillManifestLoader.Load(SkillFixtures.Folder).Loaded.Single(static b => string.Equals(b.Manifest.Name, "review", StringComparison.Ordinal));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SkillTrialPlan At(int version) => SkillTrialRecipe.Build(_review, ["pr"], foldered: false, version).Value;

    private static async Task<RegistryHarness> Started()
    {
        var r = new RegistryHarness(static s =>
        {
            foreach (var block in SkillManifestLoader.Load(SkillFixtures.Folder).Loaded)
            {
                _ = s.AddBlockDescriptor(block.Descriptor);
            }

            _ = s.AddScoped<RecipeRegistrar>().AddScoped<RecipeApprovalRecorder>();
        });
        _ = await r.H.HoldLease();
        return r;
    }

    // Another run's trial of the same block with a skills folder, registered at a version: same id, other text.
    private static async Task Occupy(RegistryHarness r, int version)
    {
        var other = SkillTrialRecipe.Build(_review, ["pr"], foldered: true, version).Value;
        _ = await r.Register(RecipeReaderTests.Reader.Read(other.Text, other.Id.Value + ".yaml").Document!);
    }

    private static async Task<Result<RecipeVersion>> Approve(RegistryHarness r, StaleVersions store)
    {
        using var entered = r.H.Callers.Enter(RegistryHarness.Human);
        await using var scope = r.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var recipes = new TrialRecipes(store, RecipeReaderTests.Reader, sp.GetRequiredService<RecipeRegistrar>(), sp.GetRequiredService<RecipeApprovalPlanner>(), sp.GetRequiredService<RecipeApprovalRecorder>());
        return await recipes.Approve(At(1).Id, At, "tester", Token);
    }

    [Fact]
    public async Task A_trial_that_loses_the_version_race_registers_one_higher()
    {
        await using var r = await Started();
        await Occupy(r, 1);

        var approved = await Approve(r, new StaleVersions(r.Recipes, staleCalls: 1));

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        Assert.Equal(2, approved.Value.Version);
        Assert.NotNull((await r.Approvals.Latest(approved.Value.Id, approved.Value.Version, approved.Value.Hash, Token)).Value);
    }

    [Fact]
    public async Task A_trial_that_loses_the_race_twice_returns_SND_RECIPE_007()
    {
        await using var r = await Started();
        await Occupy(r, 1);

        var approved = await Approve(r, new StaleVersions(r.Recipes, staleCalls: 2));

        Assert.True(approved.IsError);
        Assert.Equal(WorkflowErrors.RecipeVersionTaken.Code, approved.Error.Code);
    }

    [Fact]
    public async Task A_trial_above_the_last_version_is_refused_with_a_fix()
    {
        await using var r = await Started();
        await Occupy(r, int.MaxValue);

        var approved = await Approve(r, new StaleVersions(r.Recipes, staleCalls: 0));

        Assert.True(approved.IsError);
        Assert.Contains("remove the old trial recipe versions of workflow.skill-trial-review", approved.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>The store as a racing run saw it: the first staleCalls reads of the versions come back empty, before the other run registered.</summary>
    private sealed class StaleVersions(IRecipeStore inner, int staleCalls) : IRecipeStore
    {
        private int _calls;

        public ValueTask<Result<RecipeRegistration>> Add(RegisteredRecipe recipe, CancellationToken cancellationToken) => inner.Add(recipe, cancellationToken);

        public ValueTask<Result<RegisteredRecipe?>> Find(RecipeHash hash, CancellationToken cancellationToken) => inner.Find(hash, cancellationToken);

        public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> All(CancellationToken cancellationToken) => inner.All(cancellationToken);

        public ValueTask<Result<IReadOnlyList<RegisteredRecipe>>> Versions(RecipeId id, CancellationToken cancellationToken) =>
            ++_calls <= staleCalls ? ValueTask.FromResult(Results.Success<IReadOnlyList<RegisteredRecipe>>([])) : inner.Versions(id, cancellationToken);
    }
}
