using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Starts the Temporal workflow of a recorded run (a port so the starter is testable without a Temporal server).</summary>
public interface IGraphRunLauncher
{
    /// <summary>Starts the workflow whose Temporal id is the run id; the same id twice is refused by Temporal.</summary>
    /// <param name="run">The recorded run.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>Success, or the reason it did not start.</returns>
    public ValueTask<Result<bool>> Launch(WorkflowRun run, CancellationToken cancellationToken);
}
