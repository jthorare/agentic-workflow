using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The agent session behind a run (q65): the instance view carries its provider and id on the run, keeps it through a park and a takeover of that run, and a desk run has none.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentSessionHandleTests
{
    private static readonly SessionHandle _session = new(AgentId.From("claude-code"), SessionId.From("0b1c2d3e-0000-4000-8000-000000000001"));

    [Fact]
    public void An_agent_run_serves_the_session_on_its_run()
    {
        var run = RunOf(AgentRun("desk-task", suspended: false), _session);

        Assert.Equal(_session, run!.Session);
        Assert.Equal("agent", run.Runtime);
    }

    [Fact]
    public void The_session_survives_a_park_of_the_run()
    {
        var run = RunOf(AgentRun("parked-needs-answer-step", suspended: true), _session);

        Assert.Equal(_session, run!.Session);
    }

    [Fact]
    public void The_session_survives_a_takeover_of_the_run()
    {
        var run = RunOf(AgentRun("taken-over", suspended: true), _session);

        Assert.Equal(_session, run!.Session);
    }

    [Fact]
    public void A_run_whose_launch_knows_no_session_serves_none()
    {
        var run = RunOf(AgentRun("desk-task", suspended: false), session: null);

        Assert.Null(run!.Session);
    }

    [Fact]
    public void A_desk_run_has_no_run_and_so_no_session()
    {
        var status = StatusOf(StatusCases.Instance("desk-task"), _session);

        Assert.Null(status.Run);
    }

    [Fact]
    public void The_session_is_served_as_provider_and_id_next_to_the_launch()
    {
        var run = RunOf(AgentRun("desk-task", suspended: false), _session);

        Assert.Equal("""{"runtime":"agent","launch":"LCH-0123456789abcdef","session":{"provider":"claude-code","id":"0b1c2d3e-0000-4000-8000-000000000001"}}""", System.Text.Json.JsonSerializer.Serialize(run, SliceHarness.Json));
    }

    private static StatusRun? RunOf(WorkflowInstance instance, SessionHandle? session) => StatusOf(instance, session).Run;

    private static StatusFields StatusOf(WorkflowInstance instance, SessionHandle? session)
    {
        var view = InstanceViews.Of(instance, StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());
        return InstanceViews.Detail(view, new InstanceDetailContext("Title", Parent: null, new ViewCounts(0, 0, 0, Context: null), StatusCases.Now, Session: session)).Current.Status;
    }

    /// <summary>The case with its open run switched to an agent run on a known launch (the desk cases open a desk run).</summary>
    private static WorkflowInstance AgentRun(string name, bool suspended)
    {
        var instance = StatusCases.Instance(name);
        Assert.Equal(suspended, instance.Suspension is not null);   // why: the case is what the test says it is
        return instance with { Frame = instance.Frame with { Run = instance.Frame.Run! with { Runtime = StepRuntime.Agent, Launch = "LCH-0123456789abcdef", Task = null } } };
    }
}
