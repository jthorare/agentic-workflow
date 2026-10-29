using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.Evidence;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Evidence;

/// <summary>
/// The evidence recipes on the block runtime (BS-11): in the shipped build, 'combine' and 'evidence-none' open block runs, not desk tasks, and each real Step's
/// result is accepted by the engine's report validation and carries the instance to its end.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceBlockRuntimesTests
{
    [Theory]
    [InlineData("workflow.evidence-all.yaml", "combine", "step.evidence.combine")]
    [InlineData("workflow.evidence.yaml", "evidence-none", "step.evidence.none")]
    public void The_Step_opens_a_block_run_not_a_desk_task(string file, string state, string step)
    {
        var provider = ShippedComposition.Shared;
        var (recipe, context) = Read(provider, file);

        var entered = StateEntry.Enter(EngineFixture.At(recipe, state), recipe, state, context);

        Assert.Equal(step, recipe.States[state].Step!.Use.Value);
        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
    }

    [Fact]
    public async Task The_combined_summary_ends_evidence_all_resolved()
    {
        var provider = ShippedComposition.Shared;
        var (recipe, context) = Read(provider, "workflow.evidence-all.yaml");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "combine"), recipe, "combine", context);

        var run = await new EvidenceCombineStep().Run(BuildShipContexts.Of("step.evidence.combine", new Dictionary<string, string>(StringComparer.Ordinal) { ["evidenceUi"] = "{}", ["evidenceApi"] = "{}", ["evidenceDb"] = "{}" }), TestContext.Current.CancellationToken);

        Assert.Equal("done", Next(entered, recipe, context, run));
    }

    [Fact]
    public async Task A_phase_with_only_api_evidence_reaches_done_through_evidence_all()
    {
        var provider = ShippedComposition.Shared;
        var (recipe, context) = Read(provider, "workflow.evidence-all.yaml");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "combine"), recipe, "combine", context);

        var run = await new EvidenceCombineStep().Run(BuildShipContexts.Of("step.evidence.combine", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["evidenceUi"] = """{"selected":false,"kind":"ui","reason":"no screen"}""",
            ["evidenceApi"] = """{"result":"passed"}""",
            ["evidenceDb"] = """{"selected":false,"kind":"db","reason":"no store"}""",
        }), TestContext.Current.CancellationToken);

        Assert.Equal("done", Next(entered, recipe, context, run));
    }

    [Fact]
    public async Task No_evidence_needed_ends_the_route_resolved()
    {
        var provider = ShippedComposition.Shared;
        var (recipe, context) = Read(provider, "workflow.evidence.yaml");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "evidence-none"), recipe, "evidence-none", context);

        var run = await new EvidenceNoneStep().Run(BuildShipContexts.Of("step.evidence.none", new Dictionary<string, string>(StringComparer.Ordinal) { ["built"] = "{}" }), TestContext.Current.CancellationToken);

        Assert.Equal("done", Next(entered, recipe, context, run));
    }

    private static (Recipe Recipe, DecisionContext Context) Read(ServiceProvider provider, string file)
    {
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/" + file), file).Recipe;
        var runtimes = provider.GetRequiredService<BlockStepRuntimes>().Ids.ToDictionary(static id => id, static _ => StepRuntime.Block);
        return (recipe, new DecisionContext(ApprovalCheck.Holds, provider.GetRequiredService<BlockCatalog>(), runtimes, new InMemoryChildRecipes()));
    }

    // The state the engine moves to on the Step's result, after the same report validation a desk report gets.
    private static string Next(Core.Workflow.Engine.Instances.WorkflowInstance entered, Recipe recipe, DecisionContext context, BlockStepResult run)
    {
        var done = run.Result ?? throw new InvalidOperationException(run.Failure);
        var outputs = done.Outputs.ToDictionary(static o => o.Key, static o => EngineFixture.Fact(o.Value.ValueKind == JsonValueKind.String ? o.Value.GetString()! : o.Value.GetRawText()));   // why: a string element always holds its text
        var step = recipe.States[entered.State].Step!.Use;   // why: the caller entered a Step state
        var trigger = BlockStepTriggers.Completed(entered, recipe, context, new BlockCompletion(step, entered.Frame.Run!.Token, done.Outcome, outputs));   // why: a block run is open
        var after = Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(entered, recipe, trigger, GateAnswers.None, context))).Changes[0].After;
        return after.Park is { } park ? $"{after.State} ({park})" : after.State;
    }
}
