using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The pass serve runs at start: ends every 'running' run that Temporal has no workflow for and starts the next queued run of its workflow (issue 2367).</summary>
public interface IGraphRunRecovery
{
    /// <summary>One recovery pass.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many runs it ended, or why it could not finish (Temporal unreachable, a failed write); the caller tries again.</returns>
    public ValueTask<Result<int>> Run(CancellationToken cancellationToken);
}
