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

    /// <summary>
    /// The runs in 'running' that this process did not write: the ones a run of the previous process left behind. A run this process admitted or promoted is never listed, so a launch still in flight
    /// is not taken for an orphan (issue 2367). The read and the starter's own write-and-record are serialized, so a run is either listed as visible and not ours, or ours and not listed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs, oldest first, or the reason the read failed.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> RunningBeforeThisProcess(CancellationToken cancellationToken);

    /// <summary>
    /// Every run in 'running' except the ones whose Temporal start this process has not finished yet (issue 2999): the live sweep's candidates, runs of this process included.
    /// A queued run keeps its old start time when it is promoted, so the start time alone can't tell a launch in flight from a lost run.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs, oldest first, or the reason the read failed.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> RunningNotLaunching(CancellationToken cancellationToken);
}
