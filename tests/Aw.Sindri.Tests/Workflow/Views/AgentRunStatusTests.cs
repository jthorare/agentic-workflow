using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>What 'workflow status' says about an Step an agent session runs (P8 U6): its runtime and launch, and nothing for a desk run.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentRunStatusTests
{
    [Fact]
    public void An_agent_run_names_its_runtime_and_launch_in_the_fields_and_the_text()
    {
        var status = StatusOf(Agent("LCH-0123456789abcdef"));

        Assert.Equal(new StatusRun("agent", "LCH-0123456789abcdef"), status.Run);
        Assert.Contains("\n  run       agent LCH-0123456789abcdef\n", status.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_agent_run_still_launching_already_names_the_launch_it_will_have()
    {
        var instance = Agent(launch: null);
        var expected = AgentLaunches.LaunchIdFor(WorkflowInstanceId.Of(instance.Address), instance.Frame.Run!.Token).Value;   // why: Agent() gives the run

        var status = StatusOf(instance);

        Assert.Equal(new StatusRun("agent", expected), status.Run);
        Assert.Contains($"\n  run       agent {expected}\n", status.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_escalated_agent_run_still_launching_names_its_own_launch()
    {
        var instance = Agent(launch: null);
        var open = instance.Frame.Run! with { Escalation = 1 };   // why: Agent() gives the run
        instance = instance with { Frame = instance.Frame with { Run = open } };
        var expected = AgentLaunches.LaunchIdFor(WorkflowInstanceId.Of(instance.Address), open.Token, escalation: 1).Value;

        Assert.Equal(new StatusRun("agent", expected), StatusOf(instance).Run);
    }

    [Fact]
    public void A_desk_run_has_no_run_line()
    {
        var status = StatusOf(StatusCases.Instance("desk-task"));

        Assert.Null(status.Run);
        Assert.DoesNotContain("  run  ", status.Text, StringComparison.Ordinal);
    }

    private static WorkflowInstance Agent(string? launch)
    {
        var desk = StatusCases.Instance("desk-task");
        return desk with { Frame = desk.Frame with { Run = desk.Frame.Run! with { Runtime = StepRuntime.Agent, Launch = launch, Task = null } } };   // why: the desk case has a run
    }

    private static StatusFields StatusOf(WorkflowInstance instance) =>
        InstanceViews.Status(InstanceViews.Of(instance, StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext()), StatusCases.Now);
}
