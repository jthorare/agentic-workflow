using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'LoadRecipe' reads the descriptors of every block the recipe names, a Step's inputs included (engine spec §5.1, E1).</summary>
[Lane(TestLane.Unit)]
public sealed class RunStepActivitiesLoadTests
{
    [Fact]
    public async Task LoadRecipe_includes_the_descriptors_of_step_inputs()
    {
        var id = RecipeId.From("workflow.inputs");
        var recipe = ARecipe.WithStepInputs(ARecipe.Minimal(id), FakeStepInput.Id);
        var version = new RecipeVersion(id, 1, RegistryData.Hash("inputs"));
        var catalog = new BlockCatalog([TestBlocks.Step("step.produce"), FakeStepInput.Descriptor, TestBlocks.Step("step.unrelated")], []);
        var profiles = new InMemoryProfiles();
        var facts = new InMemoryFactStore();
        var runner = new BlockStepRunner(new BlockStepRuntimes([]), catalog, static (_, _, _) => throw new NotSupportedException(), facts, facts, profiles);
        var activities = new RunStepActivities(new Approved(new ApprovedRecipe(version, recipe)), catalog, new ProfileWorkflowTimes(profiles, profiles), runner, NullLogger<RunStepActivities>.Instance);

        var loaded = await new ActivityEnvironment().RunAsync(() => activities.LoadRecipeAsync(version));

        Assert.Equal(["input.test.echo", "step.produce"], loaded.Blocks.Select(b => b.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private sealed class Approved(ApprovedRecipe recipe) : IApprovedRecipes
    {
        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(recipe);

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovedRecipe>>(recipe);

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion version, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Holds);
    }
}
