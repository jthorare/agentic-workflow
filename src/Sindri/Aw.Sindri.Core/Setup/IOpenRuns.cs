using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The open set (first-run spec decision 11): every launched agent session, every workflow run that is running or parked, and every running job.
/// A superset of scripts/lib/sindri-open-runs.sh, which reads only the launched sessions. A read, starting and stopping nothing.
/// </summary>
public interface IOpenRuns
{
    /// <summary>Reads the open set now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each open run; none when a restart would not wait.</returns>
    public ValueTask<Result<IReadOnlyList<OpenRun>>> Read(CancellationToken cancellationToken);
}
