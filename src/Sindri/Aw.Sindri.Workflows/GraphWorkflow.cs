using Aw.Sindri.Core.Graph.Ids;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The one workflow type that walks any graph spec (issue 2258, PR 6): reads the spec, runs the nodes in topological order (ties by spec order),
/// one activity per block execution, and mirrors every node and the run's end into the ledger. A cancel ends the run 'cancelled'.
/// </summary>
[Workflow("GraphWorkflow")]
internal sealed partial class GraphWorkflow
{
    private static readonly ActivityOptions _write = GraphActivityOptions.Write;
    private static readonly ActivityOptions _detached = GraphActivityOptions.Detached;
    private static readonly ActivityOptions _block = GraphActivityOptions.Block;

    // why: a launch hands back after its slice when the session cap is full, and the workflow asks again. A cancel never goes to the launch (it does not heartbeat, so it could not hear one):
    // the workflow lets the launch return, then stops, so the close that follows sees the session the launch made (TryCancel let the close run first and left an orphan). Sending the cancel
    // anyway raced the launch's completion, and the server refused the whole workflow task ("ACTIVITY_UNKNOWN for scheduledEventId"). The one-shot reads and the close are retried without end like the writes
    private static readonly ActivityOptions _agentLaunch = new() { StartToCloseTimeout = TimeSpan.FromMinutes(10), RetryPolicy = GraphActivityOptions.Unbounded, CancellationToken = CancellationToken.None };
    private static readonly ActivityOptions _agentRead = new() { StartToCloseTimeout = TimeSpan.FromMinutes(5), RetryPolicy = GraphActivityOptions.Unbounded };

    private WorkflowRunId _run;

    /// <summary>Walks the run's spec.</summary>
    /// <param name="input">The run.</param>
    /// <returns>The run's final status.</returns>
    [WorkflowRun]
    public async Task<GraphWorkflowResult> RunAsync(GraphWorkflowInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _ = Workflow.Patched("graph-walk-1");
        _run = input.Run;
        try
        {
            var spec = await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.LoadAsync(_run), _write).ConfigureAwait(true);
            await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.BeginAsync(_run), _write).ConfigureAwait(true);
            return await Walk(spec).ConfigureAwait(true);
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e))
        {
            await Cancelled().ConfigureAwait(true);
            throw;
        }
    }
}
