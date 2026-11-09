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
        var start = spec.Nodes.FirstOrDefault(n => n.Id == forEach && string.Equals(n.Type, ForEachType, StringComparison.Ordinal));
        if (start is null)
        {
            return Refuse($"'{forEach.Value}' is not a for_each node");
        }

        var (reached, collects) = Reach(spec, start, out var nested);
        if (nested is not null)
        {
            return Refuse($"for_each '{nested.Value.Value}' sits inside the loop of '{forEach.Value}'; loops don't nest, so start another workflow for the inner one");
        }

        if (collects.Count != 1)
        {
            return Refuse(collects.Count == 0
                ? $"for_each '{forEach.Value}' has no collect after it; end the loop with a collect node"
                : $"for_each '{forEach.Value}' reaches {collects.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} collect nodes ({string.Join(", ", collects.Select(static c => $"'{c.Id.Value}'"))}); end the loop with exactly one");
        }

        var body = spec.Nodes.Where(n => reached.Contains(n.Id)).ToList();
        var inside = new HashSet<NodeId>(reached) { forEach };
        foreach (var node in body.Append(collects[0]))
        {
            if (spec.Edges.FirstOrDefault(e => e.To == node.Id && !inside.Contains(e.From)) is { } outside)
            {
                return Refuse($"node '{node.Id.Value}' is in the loop of '{forEach.Value}' but is also fed by '{outside.From.Value}', which is not; feed it only from nodes of the loop");
            }
        }

        return new LoopShape(start, body, collects[0]);
    }

    // why: walks out from the for_each along the edges and stops at a collect, so what it reached is the body and what it stopped at are the loop's ends
    private static (HashSet<NodeId> Reached, List<GraphNode> Collects) Reach(GraphSpec spec, GraphNode start, out NodeId? nested)
    {
        nested = null;
        var reached = new HashSet<NodeId>();
        var collects = new List<GraphNode>();
        var queue = new Queue<NodeId>([start.Id]);
        while (queue.TryDequeue(out var from))
        {
            // why: the validator reports an edge to a node that is not there, so it is skipped here
            foreach (var target in spec.Edges.Where(e => e.From == from).Select(e => spec.Nodes.FirstOrDefault(n => n.Id == e.To)).OfType<GraphNode>())
            {
                if (string.Equals(target.Type, ForEachType, StringComparison.Ordinal))
                {
                    nested = target.Id;
                    return (reached, collects);
                }

                if (!string.Equals(target.Type, CollectType, StringComparison.Ordinal))
                {
                    if (reached.Add(target.Id))
                    {
                        queue.Enqueue(target.Id);
                    }
                }
                else if (!collects.Exists(c => c.Id == target.Id))
                {
                    collects.Add(target);
                }
            }
        }

        return (reached, collects);
    }

    /// <summary>The loop a node runs in.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="node">A node id.</param>
    /// <returns>The loop whose body or collect the node is, or null when it runs once.</returns>
    public static LoopShape? Enclosing(GraphSpec spec, NodeId node)
    {
        ArgumentNullException.ThrowIfNull(spec);
        foreach (var each in spec.Nodes.Where(static n => string.Equals(n.Type, ForEachType, StringComparison.Ordinal)))
        {
            var found = Find(spec, each.Id);
            if (found.IsSuccess && found.Value.Repeats(node))
            {
                return found.Value;
            }
        }

        return null;
    }

    private static Error Refuse(string message) => SindriErrors.GraphBlockFailed.With(message);
}
