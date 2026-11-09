using Aw.Sindri.Core.Graph.Loops;

namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    // why: the engine refuses a loop it can't run when the for_each starts; saying so here lets 'wf validate' catch it first
    private static void Loops(GraphSpec spec, List<GraphFinding> found)
    {
        foreach (var node in spec.Nodes.Where(static n => string.Equals(n.Type, LoopBody.ForEachType, StringComparison.Ordinal)))
        {
            if (LoopBody.Find(spec, node.Id) is { IsError: true } refused)
            {
                found.Add(Found.Error("GRAPH-LOOP-001", [node.Id.Value], refused.Error.Message, "wire the for_each through its nodes to exactly one collect, and feed the loop's nodes only from the loop"));
            }
        }

        foreach (var node in spec.Nodes.Where(n => string.Equals(n.Type, LoopBody.CollectType, StringComparison.Ordinal) && LoopBody.Enclosing(spec, n.Id) is null))
        {
            found.Add(Found.Error("GRAPH-LOOP-002", [node.Id.Value], $"collect '{node.Id.Value}' ends no loop: no for_each reaches it", "put a for_each before it, or remove it"));
        }
    }
}
