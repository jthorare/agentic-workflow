using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

internal sealed partial class GraphIterationWorkflow
{
    // why: a failed node ends the item unless it has onFailure continue; a failed condition or ask_owner with 'continue' takes no named branch, so every body node after it was skipped and the item counted as succeeded (#3344); patched so a batch started before replays as it did
    private static string? ItemFailure(GraphSpec spec, GraphNode node, int item, GraphBlockOutcome outcome)
    {
        if (outcome.Status != NodeRunStatus.Failed)
        {
            return null;
        }

        var on = $"node '{node.Id.Value}' failed on item {item.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (!string.Equals(node.OnFailure, "continue", StringComparison.Ordinal))
        {
            return $"{on}: {outcome.Error}";
        }

        return Workflow.Patched("graph-continue-nowhere-loop-1") && GraphContinueRule.LeavesNowhere(spec, node)
            ? $"{on} and has onFailure continue, but it has no 'out' edge to continue on: {outcome.Error}"
            : null;
    }
}
