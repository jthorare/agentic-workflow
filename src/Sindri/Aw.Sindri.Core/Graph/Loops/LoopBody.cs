using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Loops;

/// <summary>Finds what a 'for_each' repeats: every node reachable from it, up to the first 'collect' on each path.</summary>
public static class LoopBody
{
    /// <summary>The block type that starts a loop.</summary>
    public const string ForEachType = "for_each";

    /// <summary>The block type that ends a loop.</summary>
    public const string CollectType = "collect";

    /// <summary>Reads the loop of one 'for_each' node.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="forEach">The 'for_each' node's id.</param>
    /// <returns>The loop, or why this spec's loop can't run.</returns>
    public static Result<LoopShape> Find(GraphSpec spec, NodeId forEach)
    {
        ArgumentNullException.ThrowIfNull(spec);
        throw new NotSupportedException("red: LoopBody.Find " + forEach.Value);
    }

    /// <summary>The loop a node runs in.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="node">A node id.</param>
    /// <returns>The loop whose body or collect the node is, or null when it runs once.</returns>
    public static LoopShape? Enclosing(GraphSpec spec, NodeId node)
    {
        ArgumentNullException.ThrowIfNull(spec);
        throw new NotSupportedException("red: LoopBody.Enclosing " + node.Value);
    }
}
