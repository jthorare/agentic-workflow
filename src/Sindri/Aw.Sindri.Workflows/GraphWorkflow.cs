using Aw.Sindri.Core.Graph.Ids;
using Temporalio.Common;
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
    private static readonly RetryPolicy _unbounded = new()
    {
        InitialInterval = TimeSpan.FromMilliseconds(100),
        BackoffCoefficient = 2,
        MaximumInterval = TimeSpan.FromSeconds(30),
        MaximumAttempts = 0,
    };

    // why: store and ledger writes retry until they work, like RecordEvent
    private static readonly ActivityOptions _write = new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), RetryPolicy = _unbounded };

    // why: the same writes after a cancel must not be cancelled with the workflow
    private static readonly ActivityOptions _detached = new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), RetryPolicy = _unbounded, CancellationToken = CancellationToken.None };

    // why: one attempt, because a block may have side effects (notify); a failure is the node's failure
    private static readonly ActivityOptions _block = new() { StartToCloseTimeout = TimeSpan.FromHours(1), RetryPolicy = new RetryPolicy { MaximumAttempts = 1 } };

    // why: a launch hands back after its slice when the session cap is full, and the workflow asks again; the one-shot reads and the close are retried without end like the writes
    private static readonly ActivityOptions _agentLaunch = new() { StartToCloseTimeout = TimeSpan.FromMinutes(10), RetryPolicy = _unbounded };
    private static readonly ActivityOptions _agentRead = new() { StartToCloseTimeout = TimeSpan.FromMinutes(5), RetryPolicy = _unbounded };

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
