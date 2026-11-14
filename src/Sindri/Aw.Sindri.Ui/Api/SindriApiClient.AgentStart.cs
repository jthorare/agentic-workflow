using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Api;

/// <summary>The New agent form's calls (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<AgentStartedView>> Start(Uri api, StartAgentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<AgentStartedView>>(SindriUiErrors.BadStatus.With("the start is not wired yet"));

    /// <inheritdoc />
    public Task<Result<AgentLastStartView>> LastStart(Uri api, CancellationToken cancellationToken) =>
        Task.FromResult<Result<AgentLastStartView>>(SindriUiErrors.BadStatus.With("the last start is not wired yet"));
}
