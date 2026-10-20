using Aw.Kernel;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.Api;

/// <summary>The job calls (<see cref="SindriApiClient"/>, continued; API P6): the list, one job and the cancel.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<JobList>> Jobs(Uri api, JobQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Get<JobList>(api, JobRoutes.List(query), cancellationToken, view: true);
    }

    /// <inheritdoc />
    public Task<Result<JobView>> Job(Uri api, string id, CancellationToken cancellationToken) =>
        Get<JobView>(api, JobRoutes.One(id), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<CommandAcceptedView>> CancelJob(Uri api, string id, CancellationToken cancellationToken) =>
        Call<CommandAcceptedView>(api, HttpMethod.Post, JobRoutes.Cancel(id), cancellationToken);
}
