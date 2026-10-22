using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The service and stop commands (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<JobStarted>> RunService(Uri api, string name, CancellationToken cancellationToken) =>
        Call<JobStarted>(api, HttpMethod.Post, ServiceRoutes.Run(name), cancellationToken);

    /// <inheritdoc />
    public Task<Result<CommandAcceptedView>> PauseService(Uri api, string name, CancellationToken cancellationToken) =>
        Call<CommandAcceptedView>(api, HttpMethod.Post, ServiceRoutes.Pause(name), cancellationToken);

    /// <inheritdoc />
    public Task<Result<CommandAcceptedView>> ResumeService(Uri api, string name, CancellationToken cancellationToken) =>
        Call<CommandAcceptedView>(api, HttpMethod.Post, ServiceRoutes.Resume(name), cancellationToken);

    /// <inheritdoc />
    public Task<Result<StopAcceptedView>> StopSindri(Uri api, CancellationToken cancellationToken) =>
        Call<StopAcceptedView>(api, HttpMethod.Post, ServiceRoutes.Stop, cancellationToken);
}
