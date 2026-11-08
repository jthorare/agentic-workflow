using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflow.Choice;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Which runtime a state entry opens its run on (DT spec §8.2, W20).</summary>
[Lane(TestLane.Unit)]
public sealed class StateEntryTests
{
    private static readonly BlockId _judge = BlockId.From("step.judge.choose");

    [Fact]
    public void A_block_runtime_step_with_a_registered_runtime_opens_a_block_run()
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var context = EngineFixture.Context() with { Runtimes = new Dictionary<BlockId, StepRuntime> { [_judge] = StepRuntime.Block } };

        var entered = StateEntry.Enter(EngineFixture.At(recipe, "evidence-ui"), recipe, "route-evidence", context);

        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
    }

    private static readonly BlockId _produce = BlockId.From("step.produce");

    [Fact]
    public void An_agent_step_opens_an_agent_run_when_the_build_has_its_runtime()
    {
        var context = EngineFixture.Context() with { Runtimes = new Dictionary<BlockId, StepRuntime> { [_produce] = StepRuntime.Agent } };

        var entered = StateEntry.Enter(EngineFixture.At(EngineFixture.Step, "produce"), EngineFixture.Step, "produce", context);

        Assert.Equal(StepRuntime.Agent, entered.Frame.Run?.Runtime);
        Assert.Null(entered.Frame.Run?.Task);
    }

    [Fact]
    public void A_desk_capable_agent_step_runs_at_the_desk_without_a_target()
    {
        var entered = StateEntry.Enter(EngineFixture.At(EngineFixture.Step, "produce"), EngineFixture.Step, "produce", EngineFixture.Context());

        Assert.Equal(StepRuntime.Desk, entered.Frame.Run?.Runtime);
    }

    [Fact]
    public void A_block_step_still_opens_a_block_run()
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var context = EngineFixture.Context() with { Runtimes = new Dictionary<BlockId, StepRuntime> { [_judge] = StepRuntime.Block, [_produce] = StepRuntime.Agent } };

        var entered = StateEntry.Enter(EngineFixture.At(recipe, "evidence-ui"), recipe, "route-evidence", context);

        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
    }

    [Fact]
    public void Entering_a_state_records_it_once_in_first_entry_order()
    {
        var recipe = EvalComposition.Recipe(RouteRecipes.Dev);
        var context = EngineFixture.Context() with { Runtimes = new Dictionary<BlockId, StepRuntime> { [_judge] = StepRuntime.Block } };
        var first = StateEntry.Enter(EngineFixture.At(recipe, "evidence-ui"), recipe, "route-evidence", context);

        var again = StateEntry.Enter(StateEntry.Enter(first, recipe, "evidence-ui", context), recipe, "route-evidence", context);

        Assert.Equal(["route-evidence", "evidence-ui"], again.Visited);
    }
}
