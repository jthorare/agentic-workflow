using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Layout;

namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>Where Sindri draws each node of a spec: <see cref="GraphLayout"/>'s answer, in the order of the spec's nodes.</summary>
internal static class WorkflowPositions
{
    /// <summary>The positions of a spec's nodes.</summary>
    /// <param name="spec">The graph.</param>
    /// <returns>One position per distinct node id.</returns>
    internal static IReadOnlyList<NodePosition> Of(GraphSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var layout = GraphLayout.Compute(spec);
        return [.. spec.Nodes.Select(static n => n.Id).DistinctBy(static id => id.Value, StringComparer.Ordinal).Where(id => layout.ContainsKey(id.Value)).Select(id => new NodePosition(id, layout[id.Value].X, layout[id.Value].Y))];
    }
}
