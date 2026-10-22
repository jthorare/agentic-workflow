using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Core.Views;

/// <summary>The read port over the 'jobs' view (API P6): the job queries, the cancel check, the runtime's recovery and the services list read jobs through it.</summary>
public interface IJobViews
{
    /// <summary>One job.</summary>
    /// <param name="job">The job.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, or null when there is none.</returns>
    public ValueTask<Result<JobRow?>> Get(JobId job, CancellationToken cancellationToken);

    /// <summary>Every job that has not ended: running or waiting on a signal.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows, oldest first.</returns>
    public ValueTask<Result<IReadOnlyList<JobRow>>> Running(CancellationToken cancellationToken);

    /// <summary>The jobs in a state and of a kind (either may be null for any), newest first.</summary>
    /// <param name="state">The state, or null.</param>
    /// <param name="kind">The kind, or null.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    public ValueTask<Result<IReadOnlyList<JobRow>>> List(JobState? state, JobKind? kind, CancellationToken cancellationToken);
}
