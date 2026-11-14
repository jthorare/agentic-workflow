using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A cancel: the node that was running is 'cancelled', the rest 'skipped', and the run ends 'cancelled'; the writes are not cancelled with the workflow.</summary>
internal sealed partial class GraphWorkflow
{
    private async Task Cancelled()
    {
        if (_current is { } running)
        {
            await Record(Row(running, NodeRunStatus.Cancelled), _detached).ConfigureAwait(true);
        }

        await SkipRest(_detached).ConfigureAwait(true);
        await Call((GraphRunActivities a) => a.FinishAsync(new GraphFinish(_run, WorkflowRunStatus.Cancelled, "cancelled")), detached: true).ConfigureAwait(true);
    }
}
