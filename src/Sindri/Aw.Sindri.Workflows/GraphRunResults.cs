using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Reads the finished run a 'workflow_result' node asks for, over the workflow and run stores. It never starts a run.</summary>
/// <param name="workflows">The workflows.</param>
/// <param name="runs">The runs.</param>
internal sealed class GraphRunResults(IWorkflowStore workflows, IWorkflowRunStore runs) : IGraphResultPort
{
    public ValueTask<Result<GraphRunResult>> Read(WorkflowSlug workflow, WorkflowRunId? run, CancellationToken cancellationToken)
    {
        _ = (workflows, runs, workflow, run, cancellationToken);   // stub until the read is written (#2258)
        throw new NotSupportedException();
    }
}
