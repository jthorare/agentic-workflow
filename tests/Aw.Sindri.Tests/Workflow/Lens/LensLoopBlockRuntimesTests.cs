using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Lens;

/// <summary>
/// The lens loops on the block runtime (BS-4): in the shipped build, 'lens-log' of the design recipe opens a block run, not a desk task, and the real
/// Step's result carries the instance on, with the recipe's own loop cap deciding between revise and the lens desk.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LensLoopBlockRuntimesTests
{
    [Fact]
    public async Task Review_lens_log_stays_at_the_desk_because_its_descriptor_does_not_read_the_queue()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Recipe(provider, "sindri/workflow.review-loop.yaml");

        var entered = StateEntry.Enter(EngineFixture.At(recipe, "lens-log"), recipe, "lens-log", context);

        Assert.Equal(StepRuntime.Desk, entered.Frame.Run?.Runtime);   // 'step.review.log' reads lensReport and lensRounds only: it cannot know whether a lens is still queued (docs/csharp/sindri-workflow.md)
        Assert.DoesNotContain(BlockId.From("step.review.log"), context.Runtimes.Keys);
    }

    [Fact]
    public async Task Design_lens_log_runs_as_a_block_and_drives_next_clean_and_findings()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Recipe(provider, "sindri/workflow.design.yaml");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "lens-log"), recipe, "lens-log", context);

        var next = await Run(provider, "step.lens.log", """{"lens":"product","findings":[]}""", """["product","arch"]""");
        var clean = await Run(provider, "step.lens.log", """{"lens":"arch","findings":[{"severity":"LOW","text":"l"}]}""", """["arch"]""");
        var findings = await Run(provider, "step.lens.log", """{"lens":"arch","findings":[{"severity":"HIGH","text":"h"}]}""", """["arch"]""");

        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
        Assert.Equal("lens", Next(entered, recipe, context, next));
        Assert.Equal("evidence", Next(entered, recipe, context, clean));
        Assert.Equal("revise", Next(entered, recipe, context, findings));
    }

    private static async Task<BlockStepResult> Run(ServiceProvider provider, string step, string report, string queue)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>(step)
            .Run(LensStepContexts.Of(step, ("lensReport", report), ("lensRounds", "[]"), ("lensQueue", queue)), TestContext.Current.CancellationToken);
    }

    private static (Recipe Recipe, DecisionContext Context) Recipe(ServiceProvider provider, string file)
    {
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read(file), file).Recipe;
        var runtimes = provider.GetRequiredService<BlockStepRuntimes>().Ids.ToDictionary(static id => id, static _ => StepRuntime.Block);
        return (recipe, new DecisionContext(ApprovalCheck.Holds, provider.GetRequiredService<BlockCatalog>(), runtimes, new InMemoryChildRecipes()));
    }

    // The state the engine moves to on the Step's result, after the same report validation a desk report gets.
    private static string Next(WorkflowInstance entered, Recipe recipe, DecisionContext context, BlockStepResult run)
    {
        var done = run.Result ?? throw new InvalidOperationException(run.Failure);
        var outputs = done.Outputs.ToDictionary(static o => o.Key, static o => EngineFixture.Fact(o.Value.ValueKind == JsonValueKind.String ? o.Value.GetString()! : o.Value.GetRawText()));   // why: a string element always holds its text
        var step = recipe.States[entered.State].Step!.Use;   // why: the caller entered a Step state
        var trigger = BlockStepTriggers.Completed(entered, recipe, context, new BlockCompletion(step, entered.Frame.Run!.Token, done.Outcome, outputs));   // why: a block run is open
        return Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(entered, recipe, trigger, GateAnswers.None, context))).Changes[0].After.State;
    }
}
