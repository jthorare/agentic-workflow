using System.Text.Json.Nodes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Opens the loop a node of a 'for_each' body runs in, for one item.</summary>
internal static class GraphLoopScopes
{
    /// <summary>Reads the enclosing loop and the item at <paramref name="itemIndex"/> (counted from 1); the list is read the way the 'for_each' read it, from outputs that never change.</summary>
    /// <param name="outer">The scope outside the loop.</param>
    /// <param name="spec">The spec.</param>
    /// <param name="node">A node of the loop's body.</param>
    /// <param name="itemIndex">The item's position, counted from 1.</param>
    /// <returns>The loop, or the node whose output was cut when the list could not be read.</returns>
    public static (GraphLoopContext? Loop, string? CutNode) Open(GraphNodeScope outer, GraphSpec spec, NodeId node, int itemIndex)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(spec);
        var shape = LoopBody.Enclosing(spec, node) ?? throw new ApplicationFailureException($"node '{node.Value}' is not inside a for_each", nonRetryable: true);
        var config = GraphConfig.Interpolate(shape.ForEach.Config, outer.Lookup);
        if (outer.Cut is { } cut)
        {
            return (null, cut);
        }

        var item = config["items"] is JsonArray items && itemIndex >= 1 && itemIndex <= items.Count ? items[itemIndex - 1]?.DeepClone() : null;
        return (new GraphLoopContext(shape, itemIndex, item), null);
    }
}
