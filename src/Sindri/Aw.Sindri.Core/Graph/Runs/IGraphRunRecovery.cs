using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The pass serve runs at start: ends every 'running' run that Temporal has no workflow for and starts the next queued run of its workflow (issue 2367).</summary>
public interface IGraphRunRecovery
{
    /// <summary>One recovery pass.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many runs it ended, or why it could not finish (Temporal unreachable, a failed write); the caller tries again.</returns>
    public ValueTask<Result<int>> Run(CancellationToken cancellationToken);

    /// <summary>One recovery pass over the runs that started before a cutoff (a run admitted after it may still be launching).</summary>
    /// <param name="startedBefore">The cutoff: runs that started at or after it are left alone.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many runs it ended, or why it could not finish.</returns>
    public ValueTask<Result<int>> Run(DateTimeOffset startedBefore, CancellationToken cancellationToken);
}
