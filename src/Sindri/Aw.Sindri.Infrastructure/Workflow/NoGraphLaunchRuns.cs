using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The graph engine launches no agent sessions yet (the agent blocks arrive with PR 9), so no launch belongs to a graph run. PR 9 replaces this with a read of the launches its blocks record.</summary>
internal sealed class NoGraphLaunchRuns : IGraphLaunchRuns
{
    public ValueTask<Result<WorkflowRunId?>> RunOf(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<Result<WorkflowRunId?>>((WorkflowRunId?)null);
}
