using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Which graph run launched a session. The loop guard of 'workflow_run' asks it, so an agent a workflow started can't start that workflow again.</summary>
public interface IGraphLaunchRuns
{
    /// <summary>The run that launched <paramref name="launch"/>.</summary>
    /// <param name="launch">The session's launch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The run, or null when no graph run launched it (a person did, or a recipe).</returns>
    public ValueTask<Result<WorkflowRunId?>> RunOf(LaunchId launch, CancellationToken cancellationToken);
}
