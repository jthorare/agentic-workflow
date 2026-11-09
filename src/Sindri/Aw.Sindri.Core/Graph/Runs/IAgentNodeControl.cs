using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>
/// What a person or a launched session does to the 'run_agent' node a run is waiting on (PR 7): ask which one it is, take it over, hand it back, or report it. The adapter is the Temporal client:
/// each one is a query or an update on the run's workflow, so the workflow alone decides what the node's state is.
/// </summary>
public interface IAgentNodeControl
{
    /// <summary>The node the run is waiting on.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The node; null when the run waits on no 'run_agent' node; SND-API-001 when Temporal is unreachable; SND-GRAPH-101 when it has no such run.</returns>
    public ValueTask<Result<RunningAgentNode?>> Running(WorkflowRunId run, CancellationToken cancellationToken);

    /// <summary>Takes the node over: its timeout pauses and only the human reports it.</summary>
    /// <param name="run">The run.</param>
    /// <param name="node">The node.</param>
    /// <param name="itemIndex">The item's position, 0 for a node that runs once.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>Done; a refusal naming why when the run waits on no such node.</returns>
    public ValueTask<Result<Unit>> Takeover(WorkflowRunId run, NodeId node, int itemIndex, CancellationToken cancellationToken);

    /// <summary>Hands the node back: its timeout resumes with what was left.</summary>
    /// <param name="run">The run.</param>
    /// <param name="node">The node.</param>
    /// <param name="itemIndex">The item's position, 0 for a node that runs once.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>Done; a refusal naming why when the node is not taken over.</returns>
    public ValueTask<Result<Unit>> Handback(WorkflowRunId run, NodeId node, int itemIndex, CancellationToken cancellationToken);

    /// <summary>Delivers a checked report: the node ends with these output values.</summary>
    /// <param name="run">The run.</param>
    /// <param name="report">The node and its checked output values.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>Done; a refusal naming why when the node is not waiting for a report from this caller.</returns>
    public ValueTask<Result<Unit>> Report(WorkflowRunId run, AgentNodeReport report, CancellationToken cancellationToken);
}
