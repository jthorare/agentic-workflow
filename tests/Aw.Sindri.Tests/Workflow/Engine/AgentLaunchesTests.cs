using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The pure launch planner (engine P8 D4, D8; launcher L-6, L-17).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentLaunchesTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-3/step/1");
    private static readonly AgentStepTarget _target = new(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/web");

    private static AgentStepRun Run(string token = "2.1", bool writes = true) =>
        new(WorkflowInstanceId.Of(_address), _address, RunToken.From(token), new StepRef("produce", "step.produce"), writes);

    [Fact]
    public void The_launch_id_is_the_same_for_the_same_run()
    {
        Assert.Equal(AgentLaunches.LaunchIdFor(Run()), AgentLaunches.LaunchIdFor(Run()));
        Assert.NotEqual(AgentLaunches.LaunchIdFor(Run()), AgentLaunches.LaunchIdFor(Run("3.1")));
    }

    [Fact]
    public void An_unattended_request_names_the_worktree_and_the_step()
    {
        var run = Run();

        var request = AgentLaunches.RequestFor(run, _target);

        Assert.False(request.Attended);
        Assert.Equal(LaunchMode.Open, request.Mode);
        Assert.Equal(AgentLaunches.LaunchIdFor(run), request.Launch);
        Assert.Equal("/work/web", request.RepoPath);
        Assert.Equal(_address, request.Instance);
        Assert.Equal(run.Run, request.Run);
        Assert.Equal($"/work/web/.claude/worktrees/sindri-{run.Workflow.Value}-2.1", request.Worktree);
        Assert.Equal(run.Step, request.Step?.Ref);
        Assert.Null(request.Step?.Model);
    }

    private static readonly string[] _report = ["Bash(sindri workflow complete:*)", "Bash(sindri workflow status:*)", "Bash(sindri workflow events:*)"];
    private static readonly string[] _git = ["Bash(git add:*)", "Bash(git commit:*)", "Bash(git status:*)", "Bash(git diff:*)"];

    [Fact]
    public void A_step_without_write_scope_is_not_in_plan_mode_so_it_can_report_and_writes_nothing()
    {
        var policy = AgentLaunches.RequestFor(Run(writes: false), _target).Step?.Policy;

        Assert.Equal(StepPermissionMode.Default, policy?.Mode);
        Assert.Null(policy?.WritablePath);
        Assert.Equal(_report, policy!.AllowedTools);   // why: the Assert above fails first when null
    }

    [Fact]
    public void A_write_scope_step_may_write_only_its_worktree_and_commit_it()
    {
        var request = AgentLaunches.RequestFor(Run(), _target);

        Assert.Equal(StepPermissionMode.Default, request.Step?.Policy?.Mode);
        Assert.Equal(request.Worktree, request.Step?.Policy?.WritablePath);
        Assert.Equal([.. _report, .. _git], request.Step?.Policy?.AllowedTools);
    }
}
