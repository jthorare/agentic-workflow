using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Activities;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The agent runtime's node-run activities: their names, and the launch id a replay of the same node run reuses.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentNodeActivitiesTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(WorkflowSlug.From("nightly"), revision: 3, occurrence: 12);
    private static readonly NodeId _node = NodeId.From("agent");

    [Fact]
    public void The_launch_activity_id_is_agent_launch_run_node_index() =>
        Assert.Equal("agent-launch:nightly@3:12:agent:0", AgentNodeIds.LaunchActivity(_run, _node, 0));

    [Fact]
    public void A_replay_of_the_same_node_run_gets_the_same_launch_id_and_another_item_index_gets_another()
    {
        Assert.Equal(AgentNodeIds.LaunchActivity(_run, _node, 2), AgentNodeIds.LaunchActivity(_run, _node, 2));
        Assert.NotEqual(AgentNodeIds.LaunchActivity(_run, _node, 1), AgentNodeIds.LaunchActivity(_run, _node, 2), StringComparer.Ordinal);
    }

    [Fact]
    public void The_activities_are_named_LaunchAgentNode_and_WatchAgentNode()
    {
        static string? NameOf(string method) => typeof(AgentNodeActivities).GetMethod(method, BindingFlags.Public | BindingFlags.Instance)?.GetCustomAttribute<ActivityAttribute>()?.Name;

        Assert.Equal("LaunchAgentNode", NameOf(nameof(AgentNodeActivities.LaunchNodeAsync)));
        Assert.Equal("WatchAgentNode", NameOf(nameof(AgentNodeActivities.WatchNodeAsync)));
    }
}
