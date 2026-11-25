using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'workflow_result' node reads a finished run. It only reads: it starts nothing.</summary>
public interface IGraphResultPort
{
    /// <summary>Reads the result of a run of <paramref name="workflow"/>.</summary>
    /// <param name="workflow">The workflow whose run is read.</param>
    /// <param name="run">The run; null for the newest run of the workflow that succeeded.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The result; SND-GRAPH-124 when the run is not that workflow's or not finished, or the workflow has no succeeded run; SND-GRAPH-008 when there is no such workflow.</returns>
    public ValueTask<Result<GraphRunResult>> Read(WorkflowSlug workflow, WorkflowRunId? run, CancellationToken cancellationToken);
}
