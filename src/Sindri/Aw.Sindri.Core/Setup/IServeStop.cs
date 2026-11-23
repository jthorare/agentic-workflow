using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>Stops this serve the way POST /sindri/stop does (first-run spec decision 11): the second half of a restart, once the open set is empty.</summary>
public interface IServeStop
{
    /// <summary>Records the stop and lets go of the lease, then shuts this process down.</summary>
    /// <param name="cancellationToken">Cancels before the point of no return.</param>
    /// <returns>What was released, or the error the stop met.</returns>
    public ValueTask<Result<ServeStopped>> Stop(CancellationToken cancellationToken);
}
