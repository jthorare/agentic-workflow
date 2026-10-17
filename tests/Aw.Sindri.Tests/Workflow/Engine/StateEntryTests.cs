using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
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
        var context = EngineFixture.Context() with { BlockRuntimes = new HashSet<BlockId> { _judge } };

        var entered = StateEntry.Enter(EngineFixture.At(recipe, "evidence-ui"), recipe, "route-evidence", context);

        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
    }
}
