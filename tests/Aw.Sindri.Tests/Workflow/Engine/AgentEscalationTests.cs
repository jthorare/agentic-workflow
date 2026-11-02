using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Model-pick M-1b (spec 5, 7.1): an escalated run has its own launch id, every id that exists today is unchanged, and a failed agent run says why it failed.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentEscalationTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-01JN3/scoping/1");
    private static readonly WorkflowInstanceId _workflow = WorkflowInstanceId.Of(_address);
    private static readonly RunToken _run = RunToken.For(5, 1);

    private static string Id(string text) => "LCH-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];

    [Fact]
    public void The_first_launch_keeps_the_id_it_has_always_had()
    {
        Assert.Equal(Id(_workflow.Value + ":" + _run.Value), AgentLaunches.LaunchIdFor(_workflow, _run).Value);
        Assert.Equal(Id(_workflow.Value + ":" + _run.Value), AgentLaunches.LaunchIdFor(_workflow, _run, escalation: 0).Value);
    }

    [Fact]
    public void An_escalation_hashes_the_escalation_number_into_the_id()
    {
        Assert.Equal(Id(_workflow.Value + ":" + _run.Value + ":escalation:1"), AgentLaunches.LaunchIdFor(_workflow, _run, escalation: 1).Value);
        Assert.False(string.Equals(AgentLaunches.LaunchIdFor(_workflow, _run).Value, AgentLaunches.LaunchIdFor(_workflow, _run, escalation: 1).Value, StringComparison.Ordinal));
    }

    [Fact]
    public void A_run_reads_its_escalation_when_it_names_its_launch()
    {
        var first = new AgentStepRun(_workflow, _address, _run, new StepRef("draft", "step.scope.draft"), WriteScope: false);
        var retry = first with { Escalation = 1 };

        Assert.Equal(AgentLaunches.LaunchIdFor(_workflow, _run), AgentLaunches.LaunchIdFor(first));
        Assert.Equal(AgentLaunches.LaunchIdFor(_workflow, _run, escalation: 1), AgentLaunches.LaunchIdFor(retry));
    }

    [Theory]
    [InlineData(true, false, AgentSessionEnd.Exit, AgentFailureKinds.Refused)]
    [InlineData(false, true, AgentSessionEnd.Exit, AgentFailureKinds.CheckFailed)]
    [InlineData(false, false, AgentSessionEnd.Exit, AgentFailureKinds.Crashed)]
    [InlineData(false, false, AgentSessionEnd.Lost, AgentFailureKinds.Crashed)]
    [InlineData(false, false, AgentSessionEnd.Idle, AgentFailureKinds.Crashed)]
    public void A_failed_agent_run_is_refused_check_failed_or_crashed(bool refused, bool checkFailed, AgentSessionEnd end, string expected) =>
        Assert.Equal(expected, AgentFailureKinds.Of(new AgentSessionState(end, ExitCode: null, Detail: null, Refused: refused).Refused, checkFailed));
}
