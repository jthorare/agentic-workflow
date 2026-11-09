using Aw.Sindri.Core.Graph;

namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>
/// Where Sindri draws each node of a spec. This is the one seam for the layout: it answers no positions until GraphLayout (Core/Graph/Layout) is merged, and then it calls that.
/// </summary>
internal static class WorkflowPositions
{
    /// <summary>The positions of a spec's nodes.</summary>
    /// <param name="spec">The graph.</param>
    /// <returns>One position per node; none until the layout is wired.</returns>
    internal static IReadOnlyList<NodePosition> Of(GraphSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return [];
    }
}
