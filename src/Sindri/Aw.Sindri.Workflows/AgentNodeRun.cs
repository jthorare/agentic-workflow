using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentNode' is given: the node run and the config its prompt and outputs come from.</summary>
/// <param name="Address">The node run.</param>
/// <param name="Config">The node's config with every reference replaced.</param>
internal sealed record AgentNodeRun(NodeRunAddress Address, RunAgentConfig Config)
{
    /// <summary>The id the launch activity runs under, so a replay reaches the same session.</summary>
    public string LaunchActivityId => AgentNodeIds.LaunchActivity(Address.Run, Address.Node, Address.Index);
}
