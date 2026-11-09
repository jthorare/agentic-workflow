using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A batch of a 'for_each': runs the loop's nodes for a fixed number of items, in its own history.</summary>
[Workflow("GraphIteration")]
internal sealed class GraphIterationWorkflow
{
    /// <summary>Runs the batch.</summary>
    /// <param name="input">The batch.</param>
    /// <returns>How it ended.</returns>
    [WorkflowRun]
    public Task<GraphIterationResult> RunAsync(GraphIterationInput input) => throw new NotSupportedException("red: " + input);
}
