using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The scoping station on the block runtime (scoping spec SC-R, BS-1): in the shipped build, the recipe's pure Steps open block runs, not desk tasks, and the
/// real Steps' results are accepted by the engine's report validation and carry the instance on to the next state.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ScopingBlockRuntimesTests
{
    private static readonly string[] _registered =
    [
        "step.brief.seed", "step.build.prepare", "step.interview.record", "step.phase.drift", "step.scope.anchor", "step.scope.challenge-log", "step.scope.check", "step.scope.gather",
        "step.scope.intake", "step.scope.propose", "step.scope.read-brief", "step.scope.reanchor", "step.scope.withdraw", "step.ship.return",
    ];

    [Fact]
    public void Every_registered_scoping_Step_opens_a_block_run_and_the_rest_stay_off_it()
    {
        using var provider = ShippedComposition.Compose();
        var (recipe, context) = Scoping(provider);

        var runtimes = recipe.States.Where(static s => s.Value.Step is not null).ToDictionary(static s => s.Key, s =>
            StateEntry.Enter(EngineFixture.At(recipe, s.Key), recipe, s.Key, context).Frame.Run?.Runtime, StringComparer.Ordinal);

        Assert.All(recipe.States.Where(static s => s.Value.Step is not null), s =>
            Assert.Equal(context.Runtimes.ContainsKey(s.Value.Step!.Use) ? StepRuntime.Block : StepRuntime.Desk, runtimes[s.Key]));
        Assert.Contains(StepRuntime.Block, runtimes.Values);
    }

    [Fact]
    public void The_registered_Steps_are_the_ones_this_build_runs_and_the_recipe_uses_each()
    {
        using var provider = ShippedComposition.Compose();
        var (recipe, context) = Scoping(provider);

        var scoping = context.Runtimes.Keys.Select(static k => k.Value).Where(static k => !k.StartsWith("step.lens.", StringComparison.Ordinal));   // the lens loop's block Step is the design recipe's
        Assert.Equal(_registered, scoping.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var used = recipe.States.Values.Select(static s => s.Step?.Use.Value).OfType<string>().ToHashSet(StringComparer.Ordinal);
        Assert.Empty(_registered.Where(static id => id.StartsWith("step.scope.", StringComparison.Ordinal) || string.Equals(id, "step.brief.seed", StringComparison.Ordinal) || string.Equals(id, "step.interview.record", StringComparison.Ordinal)).Except(used.Concat(["step.brief.seed", "step.scope.anchor"]), StringComparer.Ordinal));   // brief.seed and anchor are the idea child's (workflow.idea-to-brief), not this recipe's
    }

    [Fact]
    public async Task Intake_completes_on_the_block_runtime_and_the_engine_sends_a_brief_to_its_reader()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Scoping(provider);
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "intake"), recipe, "intake", context);
        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
        await using var scope = provider.CreateAsyncScope();

        var run = await scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>("step.scope.intake")
            .Run(ScopingStepContexts.Of("step.scope.intake", new ScopingFacts { ["scopeInput"] = """{"kind":"brief","repo":"fixture-notes","path":"expiry.md"}""" }), TestContext.Current.CancellationToken);

        Assert.Equal("read-brief", Next(entered, recipe, context, run));
    }

    [Fact]
    public async Task Check_on_the_gold_facts_completes_on_the_block_runtime_and_the_engine_moves_on_to_the_challenger()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Scoping(provider);
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "check"), recipe, "check", context);
        await using var scope = provider.CreateAsyncScope();

        var run = await scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>("step.scope.check")
            .Run(ScopingStepContexts.Of("step.scope.check", ScopingFacts.Expiry()), TestContext.Current.CancellationToken);

        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
        Assert.Equal("pass", run.Result?.Outcome.Value);
        Assert.NotEqual("check", Next(entered, recipe, context, run), StringComparer.Ordinal);
    }

    private static (Recipe Recipe, DecisionContext Context) Scoping(ServiceProvider provider)
    {
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.scoping.yaml"), "workflow.scoping.yaml").Recipe;
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
