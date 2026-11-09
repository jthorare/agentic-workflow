using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Starts the workflow a 'run_workflow' node names (agent-first PR 9) and, with 'wait', polls its run until it ends.</summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs; a waiting node reads its called run here.</param>
/// <param name="starter">Starts, queues or skips the run.</param>
/// <param name="profile">The approved profile, for 'workflow.callDepth'.</param>
/// <param name="clock">The clock a wait sleeps on.</param>
internal sealed class GraphWorkflowCaller(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter, IApprovedProfile profile, TimeProvider clock) : IGraphCallPort
{
    public ValueTask<int> MaxDepth(CancellationToken cancellationToken)
    {
        _ = profile;
        throw new NotSupportedException("GraphWorkflowCaller is not written yet");
    }

    public ValueTask<Result<GraphCalled>> Start(GraphCall call, CancellationToken cancellationToken)
    {
        _ = (workflows, runs, starter, clock);
        throw new NotSupportedException("GraphWorkflowCaller is not written yet");
    }

    public ValueTask<Result<GraphCallEnd>> Wait(WorkflowRunId run, TimeSpan timeout, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GraphWorkflowCaller is not written yet");
}
