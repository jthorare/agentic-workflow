using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentNode' is given: the node run and the config its prompt and outputs come from.</summary>
/// <param name="Address">The node run.</param>
/// <param name="Config">The node's config with every reference replaced.</param>
internal sealed record AgentNodeRun(NodeRunAddress Address, RunAgentConfig Config);
