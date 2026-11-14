using Aw.Kernel;

namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>Port: the start route of the New agent form (agents spec §15.4, A7). A narrow seam so the form builds before A7's client lands; ISindriApi gets the real call when A7's types do.</summary>
internal interface INewAgentRoute
{
    /// <summary>POST /sindri/sessions. Sent with the form's actId; a replay after a 2xx returns the first result and starts nothing.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The start.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The Starting session, SND-AGENT-012/013/014/017, SND-AUTH-002, or SND-UI-002/003.</returns>
    public Task<Result<AgentStartedView>> Start(Uri api, StartAgentRequest request, CancellationToken cancellationToken);
}
