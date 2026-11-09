using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// How a node run is named on the report's wire (PR 7). The report keeps the desk route's shape ('workflow complete &lt;run id&gt; --run &lt;token&gt;'), so the token is
/// '&lt;node position&gt;.&lt;item index + 1&gt;': the node's place in the spec (from 1) and the item's number (from 1).
/// </summary>
public static class RunAgentWire
{
    /// <summary>The token of a node run.</summary>
    /// <param name="spec">The run's spec.</param>
    /// <param name="node">The node.</param>
    /// <param name="itemIndex">The item's position, 0 for a node that runs once.</param>
    /// <returns>The token; null when the spec has no such node.</returns>
    public static RunToken? TokenOf(GraphSpec spec, NodeId node, int itemIndex)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentOutOfRangeException.ThrowIfNegative(itemIndex);
        for (var i = 0; i < spec.Nodes.Count; i++)
        {
            if (spec.Nodes[i].Id == node)
            {
                return RunToken.For(i + 1, itemIndex + 1);
            }
        }

        return null;
    }

    /// <summary>The node a token names.</summary>
    /// <param name="spec">The run's spec.</param>
    /// <param name="token">The token.</param>
    /// <returns>The node and the item's position; null when the token names no node of the spec.</returns>
    public static (GraphNode Node, int ItemIndex)? NodeOf(GraphSpec spec, RunToken token)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return token.Entry <= spec.Nodes.Count ? (spec.Nodes[token.Entry - 1], token.Number - 1) : null;
    }
}
