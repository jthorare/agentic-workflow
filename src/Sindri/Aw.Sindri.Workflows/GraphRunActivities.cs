using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

// why: nothing registers the activities yet (WorkerActivities gets them with the green commit); the pragma goes with that registration.
#pragma warning disable MA0182
/// <summary>
/// The activities that read and write a run (issue 2258, PR 6): load its spec, mark it running, record each node run and mirror it into the ledger,
/// and end it (which also starts the next queued run of a 'queue' workflow).
/// </summary>
/// <param name="runs">Where runs and node runs live.</param>
/// <param name="workflows">Where the revisions live.</param>
/// <param name="queue">Starts the next queued run when one ends.</param>
internal sealed class GraphRunActivities(IWorkflowRunStore runs, IWorkflowStore workflows, IGraphQueueAdvance queue)
{
    /// <summary>Reads the spec of the revision the run walks.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The spec.</returns>
    [Activity("GraphLoad")]
    public Task<GraphSpec> LoadAsync(WorkflowRunId run)
    {
        _ = (runs, workflows, run);
        throw new NotSupportedException();
    }

    /// <summary>Marks the run running and mirrors it as started.</summary>
    /// <param name="run">The run.</param>
    /// <returns>Completes when both are written.</returns>
    [Activity("GraphBegin")]
    public Task BeginAsync(WorkflowRunId run) => throw new NotSupportedException();

    /// <summary>Writes one node run row and mirrors it as started, finished or failed.</summary>
    /// <param name="nodeRun">The node run.</param>
    /// <returns>Completes when both are written.</returns>
    [Activity("GraphRecordNode")]
    public Task RecordNodeAsync(NodeRun nodeRun) => throw new NotSupportedException();

    /// <summary>Gives the run its final status, mirrors it, then starts the oldest queued run of the same workflow.</summary>
    /// <param name="finish">How the run ended.</param>
    /// <returns>Completes when the status is written, mirrored, and the next queued run is started.</returns>
    [Activity("GraphFinish")]
    public Task FinishAsync(GraphFinish finish)
    {
        _ = (finish, queue);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
