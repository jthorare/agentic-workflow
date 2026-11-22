using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Api;

/// <summary>The New agent form's calls (<see cref="SindriApiClient"/>, continued; agents spec §15.4, A7).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<AgentStartedView>> Start(Uri api, StartAgentRequest request, CancellationToken cancellationToken) =>
        Call<AgentStartedView>(api, HttpMethod.Post, SessionRoutes.Start, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<AgentLastStartView>> LastStart(Uri api, CancellationToken cancellationToken) =>
        Get<AgentLastStartView>(api, SessionRoutes.LastStart, cancellationToken);
}
