using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Docs;

/// <summary>
/// The docs-sync recipe's 'check' on the block runtime (BS-11): in the shipped build it opens a block run, not a desk task, and the real Step's result is accepted by
/// the engine's report validation: a pass goes on to opening the docs PR, a fail goes back to update while the edit loop is below its cap.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class DocsBlockRuntimesTests
{
    [Fact]
    public void The_check_Step_opens_a_block_run_not_a_desk_task()
    {
        var provider = ShippedComposition.Shared;
        var (recipe, context) = Read(provider, "workflow.docs-sync.yaml");

        var entered = StateEntry.Enter(EngineFixture.At(recipe, "check"), recipe, "check", context);

        Assert.Equal("step.docs.check", recipe.States["check"].Step!.Use.Value);
        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
    }

    [Theory]
    [InlineData("docs/a.md", "opening")]
    [InlineData("src/App.cs", "update")]
    public async Task The_result_carries_the_instance_to_opening_or_back_to_update(string changed, string expected)
    {
        var provider = ShippedComposition.Shared;
        var (recipe, context) = Read(provider, "workflow.docs-sync.yaml");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "check"), recipe, "check", context);
        var diff = new FakeRunDiff();
        diff.Add("/work/docs");
        diff.Touch("/work/docs", changed);

        var run = await new DocsCheckStep(diff, new InMemoryBriefFiles().With("docs/a.md", "# A\n")).Run(BuildShipContexts.Of("step.docs.check", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["docsScope"] = """{"base":"abc","paths":["docs/**"],"candidates":[]}""",
            ["workspace"] = "\"/work/docs\"",
        }), TestContext.Current.CancellationToken);

        Assert.Equal(expected, Next(entered, recipe, context, run));
    }

    private static (Recipe Recipe, DecisionContext Context) Read(ServiceProvider provider, string file)
    {
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/" + file), file).Recipe;
        var runtimes = provider.GetRequiredService<BlockStepRuntimes>().Ids.ToDictionary(static id => id, static _ => StepRuntime.Block);
        return (recipe, new DecisionContext(ApprovalCheck.Holds, provider.GetRequiredService<BlockCatalog>(), runtimes, new InMemoryChildRecipes()));
    }

    // The check's pass edge runs the diff-scope and secret-scan gates before the stacked PR opens; this test is about the Step's outcome, so both pass.
    private static GateAnswers PassingGates { get; } = new(
        new Dictionary<BlockId, GateResult>
        {
            [BlockId.From("gate.pr.diff-scope")] = new(GateVerdict.Pass, Score: null, Reasons: []),
            [BlockId.From("gate.pr.secret-scan")] = new(GateVerdict.Pass, Score: null, Reasons: []),
        },
        new Dictionary<EvidenceCheck, EvidenceResult>());

    // The state the engine moves to on the Step's result, after the same report validation a desk report gets.
    private static string Next(Core.Workflow.Engine.Instances.WorkflowInstance entered, Recipe recipe, DecisionContext context, BlockStepResult run)
    {
        var done = run.Result ?? throw new InvalidOperationException(run.Failure);
        var outputs = done.Outputs.ToDictionary(static o => o.Key, static o => EngineFixture.Fact(o.Value.ValueKind == JsonValueKind.String ? o.Value.GetString()! : o.Value.GetRawText()));   // why: a string element always holds its text
        var step = recipe.States[entered.State].Step!.Use;   // why: the caller entered a Step state
        var trigger = BlockStepTriggers.Completed(entered, recipe, context, new BlockCompletion(step, entered.Frame.Run!.Token, done.Outcome, outputs));   // why: a block run is open
        var after = Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(entered, recipe, trigger, PassingGates, context))).Changes[0].After;
        return after.Park is { } park ? $"{after.State} ({park})" : after.State;
    }
}
