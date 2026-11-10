using Aw.Sindri.Core.Graph.Loops;

namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    private static readonly HashSet<string> _parking = new(["wait", "ask_owner"], StringComparer.Ordinal);

    // why: the engine refuses a loop it can't run when the for_each starts; saying so here lets 'wf validate' catch it first
    private static void Loops(GraphSpec spec, List<GraphFinding> found)
    {
        foreach (var node in spec.Nodes.Where(static n => string.Equals(n.Type, LoopBody.ForEachType, StringComparison.Ordinal)))
        {
            var loop = LoopBody.Find(spec, node.Id);
            if (loop.IsSuccess && LoopBody.AgentRefusal(loop.Value) is { } agentInLoop)
            {
                found.Add(Found.Error(LoopBody.AgentInLoopCode, [node.Id.Value], agentInLoop, "move the run_agent out of the loop, or run it in another workflow started with run_workflow"));
            }

            if (loop is { IsError: true } refused)
            {
                found.Add(Found.Error("GRAPH-LOOP-001", [node.Id.Value], refused.Error.Message, "wire the for_each through its nodes to exactly one collect, and feed the loop's nodes only from the loop"));
                continue;
            }

            // why: each item runs in a GraphIteration child workflow, which runs blocks but never parks, so a wait would end at once and an ask_owner would never ask
            foreach (var parked in loop.Value.Body.Where(static n => _parking.Contains(n.Type)))
            {
                found.Add(Found.Error("GRAPH-LOOP-004", [parked.Id.Value], $"'{parked.Type}' node '{parked.Id.Value}' is inside the loop of '{node.Id.Value}': a loop's nodes can't wait or ask", "move it before the for_each or after the collect, or put the per-item part in its own workflow and call it with run_workflow"));
            }
        }

        foreach (var node in spec.Nodes.Where(n => string.Equals(n.Type, LoopBody.CollectType, StringComparison.Ordinal) && LoopBody.Enclosing(spec, n.Id) is null))
        {
            found.Add(Found.Error("GRAPH-LOOP-002", [node.Id.Value], $"collect '{node.Id.Value}' ends no loop: no for_each reaches it", "put a for_each before it, or remove it"));
        }
    }
}
