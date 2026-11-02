using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Api;

/// <summary>The setup read (<see cref="SindriApiClient"/>, continued; setup-once spec §7).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<SetupView>> Setup(Uri api, CancellationToken cancellationToken) => Get<SetupView>(api, SetupRoutes.Read, cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<JobStarted>> AddRepo(Uri api, AddRepoRequest request, CancellationToken cancellationToken) =>
        Call<JobStarted>(api, HttpMethod.Post, SetupRoutes.Repos, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<JobStarted>> RegisterMcp(Uri api, RegisterMcpRequest request, CancellationToken cancellationToken) =>
        Call<JobStarted>(api, HttpMethod.Post, SetupRoutes.Mcp, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

}
