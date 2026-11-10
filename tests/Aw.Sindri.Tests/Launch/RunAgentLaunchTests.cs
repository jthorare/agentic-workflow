using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A 'run_agent' node's launch (PR 7): the node-run address, the branch and session names, and the unattended request.</summary>
[Lane(TestLane.Unit)]
public sealed class RunAgentLaunchTests
{
    private static readonly NodeRunAddress _address = NodeRunAddress.Of(WorkflowRunId.From("walk@1:12"), NodeId.From("check"));
    private static readonly AgentStepTarget _target = new(ItemId.From("ITM-0192f0c1a2b37c4d8e9f00112233aabb"), AgentId.From("claude-code"), "/work/web", RepoName.From("web"));

    private static RunAgentConfig Config(string? agent = null) =>
        new("web", [], agent, "check it", new Dictionary<string, string>(StringComparer.Ordinal) { ["ok"] = "boolean" }, TimeSpan.FromMinutes(45), [], Wall: null);

    [Fact]
    public void The_branch_is_wf_slug_run_and_the_session_name_uses_the_launch_id()
    {
        var session = LaunchSessionName.For(_address.Launch());

        Assert.Equal("wf/walk/1-12-check", _address.Branch);
        Assert.Equal("wf/walk/1-12-check", _address.RefNamespace);   // the session may write its own node's refs and no other branch of the workflow
        Assert.Equal("wf/walk/1-12-check/work", _address.WorkBranch);
        Assert.StartsWith(_address.RefNamespace + "/", _address.WorkBranch, StringComparison.Ordinal);
        Assert.NotEqual(NodeRunAddress.Of(WorkflowRunId.From("walk@1:12"), NodeId.From("fix")).RefNamespace, _address.RefNamespace, StringComparer.Ordinal);
        Assert.StartsWith("snd-LCH-", session, StringComparison.Ordinal);
        foreach (var name in new[] { _address.Branch, _address.WorkBranch, session, _address.ShortRun, RunAgentLaunches.WorktreeFor(_address, "/work/web") })
        {
            Assert.DoesNotContain(':', name);
            Assert.DoesNotContain('@', name);
        }
    }

    [Fact]
    public void The_launch_id_is_the_same_on_a_replay_and_differs_per_node_and_item()
    {
        var again = NodeRunAddress.Of(WorkflowRunId.From("walk@1:12"), NodeId.From("check"));
        var other = NodeRunAddress.Of(WorkflowRunId.From("walk@1:12"), NodeId.From("fix"));
        var item = NodeRunAddress.Of(WorkflowRunId.From("walk@1:12"), NodeId.From("check"), itemIndex: 2);
        var next = NodeRunAddress.Of(WorkflowRunId.From("walk@1:13"), NodeId.From("check"));

        Assert.Equal(_address.Launch(), again.Launch());
        Assert.Equal(4, new[] { _address.Launch(), other.Launch(), item.Launch(), next.Launch() }.Distinct().Count());
        Assert.Equal("wf/walk/1-12-check-2", item.Branch);
        Assert.Equal("walk", _address.Workflow.Value);
    }

    [Fact]
    public void A_node_launch_carries_its_address_and_is_unattended_in_a_fresh_worktree()
    {
        var request = RunAgentLaunches.RequestFor(_address, _target, Config(), "the brief", ["github"]);

        Assert.Equal(_address, request.Node);
        Assert.Equal(_address.Launch(), request.Launch);
        Assert.False(request.Attended);
        Assert.Equal(LaunchMode.Open, request.Mode);
        Assert.Null(request.Instance);
        Assert.Null(request.Run);
        Assert.Equal("/work/web", request.RepoPath);
        Assert.Equal("/work/web/.claude/worktrees/wf-walk-1-12-check", request.Worktree);
        Assert.Equal(["github"], request.Task!.HostGroups);
        Assert.Equal("the brief", request.Task.Brief);
        Assert.Equal("boolean", request.Task.Outputs["ok"]);
        Assert.Equal(request.Worktree, request.Step!.Policy!.WritablePath);
        Assert.Equal(StepPermissionMode.Default, request.Step.Policy.Mode);
        Assert.Contains("Bash(git commit:*)", request.Step.Policy.AllowedTools, StringComparer.Ordinal);
        Assert.Contains("Bash(sindri workflow complete:*)", request.Step.Policy.AllowedTools, StringComparer.Ordinal);
        Assert.Contains("mcp__sindri", request.Step.Policy.AllowedTools, StringComparer.Ordinal);   // the node may call every tool of Sindri's MCP
        Assert.Equal("the brief", request.Step.Brief);
        Assert.Equal(TimeSpan.FromMinutes(45), request.StepTimeout);
    }

    [Fact]
    public void RequestFor_puts_the_provisioned_mounts_and_limits_on_the_task()
    {
        var resources = new Aw.Sindri.Core.Resources.ProvisionedResources(new Dictionary<string, string>(StringComparer.Ordinal), [new Aw.Sindri.Core.Sandbox.Plan.ResourceMount("resources", "/home/s/resources/workflow/web")], new Aw.Sindri.Core.Sandbox.Plan.ResourceLimitOverride(Memory: "2GiB"));

        var request = RunAgentLaunches.WithResources(RunAgentLaunches.RequestFor(_address, _target, Config(), "b", []), resources);

        Assert.Equal("/home/s/resources/workflow/web", Assert.Single(request.Task!.ResourceMounts!).HostPath);
        Assert.Equal("2GiB", request.Task.ResourceLimits!.Memory);
        Assert.Null(RunAgentLaunches.RequestFor(_address, _target, Config(), "b", []).Task!.ResourceMounts);
    }

    [Fact]
    public void A_nodes_agent_overrides_the_target_and_an_attended_launch_keeps_a_null_node_run()
    {
        var codex = RunAgentLaunches.RequestFor(_address, _target, Config("codex"), "b", []);
        var attended = A.LaunchRequest().Build();

        Assert.Equal(AgentId.From("codex"), codex.Agent);
        Assert.Null(attended.Node);
        Assert.Null(attended.Task);
        Assert.Null(attended.Run);
        Assert.True(attended.Attended);
    }

    [Fact]
    public void The_brief_names_the_prompt_the_outputs_and_the_exact_report_command()
    {
        var token = RunToken.For(entry: 3, number: 1);

        var brief = RunAgentBrief.Render(_address, token, Config());

        Assert.Contains("check it", brief, StringComparison.Ordinal);
        Assert.Contains("sindri workflow complete walk@1:12 --run 3.1 --outcome done --output fact:ok=$AW_SINDRI_RUN_DIR/.sindri-out/ok", brief, StringComparison.Ordinal);
        Assert.Equal(brief, RunAgentBrief.Render(_address, token, Config()));
    }

    [Fact]
    public void The_report_token_is_the_nodes_place_in_the_spec_and_the_items_number()
    {
        var spec = new GraphSpec(Version: 1, "t", Description: null, "unlimited", Resources: null,
            [new GraphNode(NodeId.From("start"), "manual", Title: null, Config: []), new GraphNode(NodeId.From("check"), "run_agent", Title: null, Config: [])], []);

        var token = RunAgentWire.TokenOf(spec, NodeId.From("check"), itemIndex: 0);

        Assert.Equal(RunToken.For(entry: 2, number: 1), token);
        Assert.Equal(RunToken.For(entry: 2, number: 3), RunAgentWire.TokenOf(spec, NodeId.From("check"), itemIndex: 2));
        Assert.Null(RunAgentWire.TokenOf(spec, NodeId.From("nope"), itemIndex: 0));
        var found = RunAgentWire.NodeOf(spec, RunToken.For(entry: 2, number: 3));
        Assert.Equal(NodeId.From("check"), found!.Value.Node.Id);
        Assert.Equal(2, found.Value.ItemIndex);
        Assert.Null(RunAgentWire.NodeOf(spec, RunToken.For(entry: 9, number: 1)));
    }
}
