namespace Aw.Sindri.Core.Graph.Loops;

/// <summary>The nodes one 'for_each' repeats.</summary>
/// <param name="ForEach">The 'for_each' node.</param>
/// <param name="Body">The nodes between the 'for_each' and its 'collect', in spec order.</param>
/// <param name="Collect">The 'collect' node that ends the loop.</param>
public sealed record LoopShape(GraphNode ForEach, IReadOnlyList<GraphNode> Body, GraphNode Collect)
{
    /// <summary>The nodes that run once per item: the body, then the collect.</summary>
    public IReadOnlyList<GraphNode> PerItem => [.. Body, Collect];

    /// <summary>Whether the node runs once per item.</summary>
    /// <param name="node">A node id.</param>
    /// <returns>True for a body node or the collect.</returns>
    public bool Repeats(Ids.NodeId node) => Body.Any(b => b.Id == node) || Collect.Id == node;
}
