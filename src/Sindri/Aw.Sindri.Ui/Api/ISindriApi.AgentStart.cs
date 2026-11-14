using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Api;

/// <summary>The New agent form's calls (<see cref="ISindriApi"/>, continued; agents spec §15.4, A7): the start (<see cref="INewAgentRoute"/>) and the last choices.</summary>
internal partial interface ISindriApi : INewAgentRoute
{
    /// <summary>GET /sindri/sessions/last-start: the repo and agent of the last 2xx start. A read.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The last choices (both null when none), or SND-UI-002/003.</returns>
    public Task<Result<AgentLastStartView>> LastStart(Uri api, CancellationToken cancellationToken);
}
