using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

// why: nothing registers the workflow yet (SindriWorker gets it with the green commit); the pragma goes with that registration.
#pragma warning disable MA0182
/// <summary>
/// The one workflow type that walks any graph spec (issue 2258, PR 6): reads the spec, runs the nodes in topological order (ties by spec order),
/// one activity per block execution, and mirrors every node and the run's end into the ledger.
/// </summary>
[Workflow("GraphWorkflow")]
internal sealed class GraphWorkflow
{
    /// <summary>Walks the run's spec.</summary>
    /// <param name="input">The run.</param>
    /// <returns>The run's final status.</returns>
    [WorkflowRun]
    public Task<GraphWorkflowResult> RunAsync(GraphWorkflowInput input) => throw new NotSupportedException();
}
#pragma warning restore MA0182
