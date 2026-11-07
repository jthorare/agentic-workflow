using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The band's cells name the step block of their state (skill flows spec §5.2, F-2a): 'step.skill.review' lets the run page tell a skill state from any other.</summary>
[Lane(TestLane.Unit)]
public sealed class StateCellBlockTests
{
    private static InstanceView View() =>
        InstanceViews.Of(EngineFixture.At(EngineFixture.Step, "produce"), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext(Version: 4));

    [Fact]
    public void A_state_with_a_step_names_its_block()
    {
        var produce = View().States.Single(static s => s.State == "produce");

        Assert.Equal(EngineFixture.Step.States["produce"].Step!.Use.Value, produce.Block);
    }

    [Fact]
    public void A_state_with_no_step_has_no_block()
    {
        var terminal = View().States.First(static s => s.Terminal);

        Assert.Null(terminal.Block);
    }

    [Fact]
    public void The_block_is_in_the_cells_JSON_only_when_there_is_one()
    {
        var states = View().States;
        var produce = Aw.Kernel.Json.AwJson.Serialize(states.Single(static s => s.State == "produce"), SliceHarness.Json);
        var terminal = Aw.Kernel.Json.AwJson.Serialize(states.First(static s => s.Terminal), SliceHarness.Json);

        Assert.Contains("\"block\":", System.Text.Encoding.UTF8.GetString(produce), StringComparison.Ordinal);
        Assert.DoesNotContain("\"block\"", System.Text.Encoding.UTF8.GetString(terminal), StringComparison.Ordinal);
    }
}
