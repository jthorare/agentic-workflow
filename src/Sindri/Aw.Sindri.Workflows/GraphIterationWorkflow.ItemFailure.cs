using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

internal sealed partial class GraphIterationWorkflow
{
    // why: a body node with onFailure skip-item that fails drops only its item: the walk records the item's nodes left 'skipped' and goes on with the next item, and nothing reaches the run's error (#3806).
    // No patch: no history before this change could hold a 'skip-item' node, since every write path refused the value (GRAPH-NODE-003)
    private static bool Drops(LoopShape shape, GraphNode node, GraphBlockOutcome outcome) =>
        outcome.Status == NodeRunStatus.Failed && string.Equals(node.OnFailure, "skip-item", StringComparison.Ordinal) && shape.Body.Any(b => b.Id == node.Id);

    // why: a failed node ends the item unless it has onFailure continue; a failed condition or ask_owner with 'continue' takes no named branch, so every body node after it was skipped and the item counted as succeeded (#3344); patched so a batch started before replays as it did
    private static string? ItemFailure(GraphSpec spec, GraphNode node, int item, GraphBlockOutcome outcome)
    {
        if (outcome.Status != NodeRunStatus.Failed)
        {
            return null;
        }

        if (!string.Equals(node.OnFailure, "continue", StringComparison.Ordinal))
        {
            return NodeFailureText.Stopped(node.Id.Value, item, outcome.Error);
        }

        return Workflow.Patched("graph-continue-nowhere-loop-1") && GraphContinueRule.LeavesNowhere(spec, node)
            ? NodeFailureText.Stranded(node.Id.Value, item, outcome.Error)
            : null;
    }
}
