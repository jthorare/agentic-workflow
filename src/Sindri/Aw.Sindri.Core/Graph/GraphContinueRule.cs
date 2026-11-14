namespace Aw.Sindri.Core.Graph;

/// <summary>The rule for 'onFailure: continue': a failed node has one path to continue on, its 'out' edge (#3003).</summary>
public static class GraphContinueRule
{
    /// <summary>A failed node takes no named branch, so it can only continue by an 'out' edge. Without one, every node after it would be skipped.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="node">The node that failed with 'onFailure: continue'.</param>
    /// <returns>True when the node has edges out and none of them is 'out'; a node with no edge out ends a branch and has nothing to skip.</returns>
    public static bool LeavesNowhere(GraphSpec spec, GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(node);
        var leaving = spec.Edges.Where(e => e.From == node.Id).ToList();
        return leaving.Count > 0 && !leaving.Exists(e => string.Equals(e.Output ?? "out", "out", StringComparison.Ordinal));
    }
}
