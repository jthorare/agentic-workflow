using Aw.Sindri.Core.Graph.Loops;

namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>
/// The graph validator (issue 2258, Part 2 "Validation"): one trigger, every node connected, no cycle, references only to
/// nodes upstream, known block types with a fitting config, a readable cron and timezone, known repos. Pure: the names it
/// can't answer for come through <see cref="GraphValidationContext"/>.
/// </summary>
/// <param name="context">What it consults besides the document.</param>
public sealed partial class GraphValidator(GraphValidationContext context)
{
    /// <summary>Checks <paramref name="spec"/>.</summary>
    /// <param name="spec">The graph.</param>
    /// <param name="mode">Draft or activation.</param>
    /// <returns>The findings.</returns>
    public GraphValidation Validate(GraphSpec spec, GraphMode mode)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var found = new List<GraphFinding>();
        var structure = new GraphStructure(spec);
        Duplicates(spec, found);
        ResourceDeclarations(spec, found);
        Edges(spec, structure, found);
        foreach (var node in spec.Nodes)
        {
            Node(node, found);
        }

        Triggers(spec, structure, found);
        Cycles(structure, found);
        Loops(spec, found);
        var loops = spec.Nodes
            .Where(static n => string.Equals(n.Type, LoopBody.ForEachType, StringComparison.Ordinal))
            .Select(n => LoopBody.Find(spec, n.Id))
            .Where(static r => r.IsSuccess)
            .Select(static r => r.Value)
            .ToList();
        foreach (var node in spec.Nodes)
        {
            References(new NodeScope(node, structure, structure.Upstream(node.Id.Value), loops), found);
        }

        return new GraphValidation(mode, found);
    }

    internal bool IsTriggerType(string type) => context.Blocks.TryGetValue(type, out var block) && block.IsTrigger;

    private static void Duplicates(GraphSpec spec, List<GraphFinding> found)
    {
        foreach (var node in spec.Nodes.Where(static n => _scopeRoots.Contains(n.Id.Value)))
        {
            found.Add(Found.Error("GRAPH-NODE-002", [node.Id.Value], $"node id '{node.Id.Value}' is reserved: references read 'trigger', 'item', 'itemIndex' and 'workflow' as themselves", "rename the node"));
        }

        foreach (var group in spec.Nodes.GroupBy(static n => n.Id.Value, StringComparer.Ordinal).Where(static g => g.Skip(1).Any()))
        {
            found.Add(Found.At("GRAPH-NODE-001", group.Key, "id", $"node id '{group.Key}' is used by more than one node", "give each node its own id"));
        }
    }

    private void Edges(GraphSpec spec, GraphStructure structure, List<GraphFinding> found)
    {
        foreach (var edge in spec.Edges)
        {
            foreach (var end in new[] { edge.From.Value, edge.To.Value }.Where(id => !structure.Has(id)))
            {
                found.Add(Found.At("GRAPH-EDGE-001", end, "edges", $"an edge names node '{end}', which does not exist", $"add a node '{end}' or remove the edge"));
            }

            if (structure.Find(edge.From.Value) is { } source && context.Blocks.TryGetValue(source.Type, out var block))
            {
                var output = edge.Output ?? "out";
                var branches = block.OutputsFor(source.Config).BranchNames;
                if (!branches.Contains(output, StringComparer.Ordinal))
                {
                    found.Add(Found.At("GRAPH-EDGE-002", source.Id.Value, "edges", $"an edge leaves '{source.Id.Value}' by '{output}', which a {source.Type} block does not have", $"use one of: {string.Join(", ", branches)}"));
                }
            }
        }
    }

    private void Triggers(GraphSpec spec, GraphStructure structure, List<GraphFinding> found)
    {
        var triggers = spec.Nodes.Where(n => context.Blocks.TryGetValue(n.Type, out var b) && b.IsTrigger).Select(static n => n.Id.Value).ToList();
        if (triggers.Count == 0)
        {
            found.Add(Found.Gap("GRAPH-TRIGGER-002", [], "this workflow has no trigger", "add a trigger node such as a schedule"));
            return;
        }

        if (triggers.Count > 1)
        {
            var names = string.Join("', '", triggers);
            found.Add(Found.Error("GRAPH-TRIGGER-001", triggers, $"a workflow has exactly one trigger, but nodes '{names}' are all triggers", $"keep one of '{names}' and delete the others"));
        }

        var reached = structure.Reach(triggers);
        foreach (var id in structure.Order.Where(id => !reached.Contains(id)).Distinct(StringComparer.Ordinal))
        {
            found.Add(Found.Gap("GRAPH-WIRING-001", [id], $"node '{id}' is not connected to the trigger", $"add an edge into '{id}' from a node that runs before it, or delete it"));
        }
    }

    private static void Cycles(GraphStructure structure, List<GraphFinding> found)
    {
        foreach (var cycle in structure.Cycles())
        {
            var path = string.Join(" -> ", [.. cycle, cycle[0]]);
            found.Add(Found.Error("GRAPH-CYCLE-001", cycle, $"these nodes run in a loop: {path}", "remove or reroute one of those edges so the path ends"));
        }
    }
}
