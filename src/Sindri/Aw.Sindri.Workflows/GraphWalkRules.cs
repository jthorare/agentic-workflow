using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The two rules that decide which node runs next, shared by the run's walk and a batch's walk of one item.</summary>
internal static class GraphWalkRules
{
    /// <summary>Every edge into the node leaves a node that is already decided (ran or was skipped).</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="node">The node.</param>
    /// <param name="decided">The nodes that ran or were skipped.</param>
    /// <returns>True when the node may be looked at.</returns>
    public static bool Ready(GraphSpec spec, GraphNode node, IReadOnlySet<NodeId> decided) => spec.Edges.Where(e => e.To == node.Id).All(e => decided.Contains(e.From));

    /// <summary>A node with no edge into it always runs; any other runs when some edge into it left by a branch its source took.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="node">The node.</param>
    /// <param name="taken">The branches each decided node took.</param>
    /// <returns>True when the node runs.</returns>
    public static bool Live(GraphSpec spec, GraphNode node, IReadOnlyDictionary<NodeId, IReadOnlyList<string>> taken)
    {
        var incoming = spec.Edges.Where(e => e.To == node.Id).ToList();
        return incoming.Count == 0 || incoming.Exists(e => taken.TryGetValue(e.From, out var branches) && branches.Contains(e.Output ?? "out", StringComparer.Ordinal));
    }
}
