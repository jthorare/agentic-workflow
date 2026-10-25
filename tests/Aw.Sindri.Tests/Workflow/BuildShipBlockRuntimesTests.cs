using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The build, ship and phase recipes on the block runtime (line spec §7, BS-3): in the shipped build, 'prepare', 'return' and 'drift' open block runs, not
/// desk tasks, and each real Step's result is accepted by the engine's report validation and carries the instance to the next state.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class BuildShipBlockRuntimesTests
{
    [Theory]
    [InlineData("workflow.build.yaml", "prepare", "step.build.prepare")]
    [InlineData("workflow.build.yaml", "amend", "step.build.amend")]
    [InlineData("workflow.build.yaml", "red-check", "step.build.red-check")]
    [InlineData("workflow.build.yaml", "checks", "step.build.checks")]
    [InlineData("workflow.ship.yaml", "green", "step.ship.green")]
    [InlineData("workflow.ship.yaml", "pack", "step.ship.pack")]
    [InlineData("workflow.ship.yaml", "freeze", "step.ship.freeze")]
    [InlineData("workflow.ship.yaml", "return", "step.ship.return")]
    [InlineData("workflow.phase.yaml", "drift", "step.phase.drift")]
    [InlineData("workflow.phase.yaml", "seed", "step.phase.seed")]
    [InlineData("workflow.prove.yaml", "intake", "step.prove.intake")]
    [InlineData("workflow.prove.yaml", "before-after", "step.prove.before-after")]
    [InlineData("workflow.prove.yaml", "fail-log", "step.prove.fail-log")]
    [InlineData("workflow.prove.yaml", "evidence-pack", "step.prove.evidence")]
    public void The_Step_opens_a_block_run_not_a_desk_task(string file, string state, string step)
    {
        using var provider = ShippedComposition.Compose();
        var (recipe, context) = Read(provider, file);

        var entered = StateEntry.Enter(EngineFixture.At(recipe, state), recipe, state, context);

        Assert.Equal(step, recipe.States[state].Step!.Use.Value);
        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
    }

    [Fact]
    public async Task A_ship_that_goes_red_returns_to_build_without_a_desk_task()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Read(provider, "workflow.ship.yaml");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "return"), recipe, "return", context);

        var run = await new ShipReturnStep().Run(BuildShipContexts.Of("step.ship.return", new Dictionary<string, string>(StringComparer.Ordinal) { ["findings"] = "[{\"rule\":\"SHIP-SECRET-001\"}]" }), TestContext.Current.CancellationToken);

        Assert.Equal("returned", Next(entered, recipe, context, run));
    }

    [Fact]
    public async Task A_head_review_moved_is_accepted_and_the_phase_leaves_drift()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Read(provider, "workflow.phase.yaml");
        var entered = StateEntry.Enter(Pinned(EngineFixture.At(recipe, "drift")), recipe, "drift", context);
        var diff = new FakeRunDiff();
        diff.Add("/work/p1", "abc");
        diff.Advance("/work/p1");

        var run = await new PhaseDriftStep(diff).Run(BuildShipContexts.Of("step.phase.drift", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["built"] = "{\"head\":\"abc\"}",
            ["change"] = "{\"head\":\"abc\"}",
            ["workspace"] = "\"/work/p1\"",
        }), TestContext.Current.CancellationToken);

        Assert.Equal("moved", run.Result?.Outcome.Value);
        Assert.NotEqual("drift", Next(entered, recipe, context, run), StringComparer.Ordinal);   // the report is accepted and leaves drift (the bare instance holds no child inputs, so the child it enters parks for them)
    }

    [Fact]
    public async Task A_proof_that_failed_is_logged_and_the_prove_recipe_ends_failed()
    {
        await using var provider = ShippedComposition.Compose();
        var (recipe, context) = Read(provider, "workflow.prove.yaml");
        var entered = StateEntry.Enter(EngineFixture.At(recipe, "fail-log"), recipe, "fail-log", context);

        var run = await new ProveFailLogStep().Run(BuildShipContexts.Of("step.prove.fail-log", new Dictionary<string, string>(StringComparer.Ordinal) { ["findings"] = "[]", ["beforeAfter"] = "{}", ["holdoutResult"] = "{}" }), TestContext.Current.CancellationToken);

        Assert.Equal("failed", Next(entered, recipe, context, run));
    }

    private static (Recipe Recipe, DecisionContext Context) Read(ServiceProvider provider, string file)
    {
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/" + file), file).Recipe;
        var runtimes = provider.GetRequiredService<BlockStepRuntimes>().Ids.ToDictionary(static id => id, static _ => StepRuntime.Block);
        return (recipe, new DecisionContext(ApprovalCheck.Holds, provider.GetRequiredService<BlockCatalog>(), runtimes, Siblings()));
    }

    // The shipped recipes as an approval pins them: the resolver for the children a wait enters, and the hash each parent holds per child.
    private static readonly Lazy<IReadOnlyList<Recipe>> _shipped = new(static () =>
        [.. ShippedRecipes.Recipes.Where(static r => r.StartsWith("sindri/", StringComparison.Ordinal)).Select(EvalComposition.Recipe)]);

    private static InMemoryChildRecipes Siblings()
    {
        var children = InMemoryChildRecipes.None;
        foreach (var recipe in _shipped.Value)
        {
            _ = children.With(new ChildRecipe(recipe, recipe.Hash!.Value));
        }

        return children;
    }

    // The instance with every shipped recipe pinned as its child (EngineFixture.At pins none for a recipe other than the item's).
    private static WorkflowInstance Pinned(WorkflowInstance instance) => instance with
    {
        Recipe = instance.Recipe with { Children = _shipped.Value.ToDictionary(static r => r.Id, static r => r.Hash!.Value) },
    };

    // The state the engine moves to on the Step's result, after the same report validation a desk report gets.
    private static string Next(WorkflowInstance entered, Recipe recipe, DecisionContext context, BlockStepResult run)
    {
        var done = run.Result ?? throw new InvalidOperationException(run.Failure);
        var outputs = done.Outputs.ToDictionary(static o => o.Key, static o => EngineFixture.Fact(o.Value.ValueKind == JsonValueKind.String ? o.Value.GetString()! : o.Value.GetRawText()));   // why: a string element always holds its text
        var step = recipe.States[entered.State].Step!.Use;   // why: the caller entered a Step state
        var trigger = BlockStepTriggers.Completed(entered, recipe, context, new BlockCompletion(step, entered.Frame.Run!.Token, done.Outcome, outputs));   // why: a block run is open
        var accepted = trigger as StepCompleted ?? throw new InvalidOperationException("the report was refused: " + (trigger as StepFailed)?.Reason);   // a refused report becomes step.failed, which the engine now retries in the same state: the test would pass or fail by the retry, not by the report
        var after = Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(entered, recipe, accepted, GateAnswers.None, context))).Changes[0].After;
        return after.Park is { } park ? $"{after.State} ({park})" : after.State;
    }
}
