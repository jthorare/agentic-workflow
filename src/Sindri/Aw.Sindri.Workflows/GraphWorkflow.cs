using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
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

    private WorkflowRunId _run;
    private int _agentLaunchAttempts = GraphAgentLoop.DefaultAgentLaunchAttempts;

    /// <summary>Walks the run's spec.</summary>
    /// <param name="input">The run.</param>
    /// <returns>The run's final status.</returns>
    [WorkflowRun]
    public async Task<GraphWorkflowResult> RunAsync(GraphWorkflowInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _ = Workflow.Patched("graph-walk-1");
        _run = input.Run;
        _agentLaunchAttempts = input.AgentLaunchAttempts > 0 ? input.AgentLaunchAttempts : GraphAgentLoop.DefaultAgentLaunchAttempts;
        _agents = new GraphAgentLoop(_run, _agentLaunchAttempts);
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
        catch (Exception e) when (GraphFaults.Ends(e))
        {
            // why: a write that ran out of retries or an exception in workflow code would otherwise leave the run 'running' for ever (a workflow-task failure is retried without end) and block its workflow's next run (issue 2999)
            return await Failed(e).ConfigureAwait(true);
        }
    }

    private async Task<GraphWorkflowResult> Failed(Exception e)
    {
        _current = null;
        try
        {
            await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.FinishAsync(new GraphFinish(_run, WorkflowRunStatus.Failed, GraphFaults.Reason(e))), _detached).ConfigureAwait(true);
        }
        catch (Exception finish) when (GraphFaults.Ends(finish))
        {
            // why: the end itself could not be written; failing the workflow closes it in Temporal, so the recovery sweep ends the row
            throw new ApplicationFailureException($"The run could not be ended: {GraphFaults.Reason(finish)}", finish);
        }

        return new GraphWorkflowResult(WorkflowRunStatus.Failed);
    }
}
