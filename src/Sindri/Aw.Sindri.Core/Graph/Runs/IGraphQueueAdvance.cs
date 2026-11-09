using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Starts the next waiting run of a 'queue' workflow once the run before it has ended (a separate port so the trigger-facing <see cref="IGraphRunStarter"/> keeps its shape).</summary>
public interface IGraphQueueAdvance
{
    /// <summary>Starts the oldest queued run of <paramref name="workflow"/>, unless a run of it is still running or waiting.</summary>
    /// <param name="workflow">The workflow whose run just ended.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The id of the run it started, or null when none was queued or another run is still in flight.</returns>
    public ValueTask<Result<WorkflowRunId?>> StartNextQueued(WorkflowId workflow, CancellationToken cancellationToken);
}
