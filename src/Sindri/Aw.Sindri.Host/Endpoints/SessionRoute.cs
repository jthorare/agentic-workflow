using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Binds the session routes' raw values (P1 binding rule): '{provider}/{id}' and the '?provider=', '?state=' and '?inWorkflow=' filters, each a 400 SND-CLI-003 when malformed.</summary>
internal static class SessionRoute
{
    /// <summary>The session named by a route's '{provider}' and '{id}' segments.</summary>
    /// <param name="provider">The agent segment.</param>
    /// <param name="id">The session id segment.</param>
    /// <returns>The ref, or SND-CLI-003.</returns>
    internal static Result<SessionRef> Ref(string provider, string id) =>
        AgentId.TryFrom(provider, out var agent) && SessionId.TryFrom(id, out var session)
            ? new SessionRef(agent, session)
            : SindriErrors.InvalidInput.With("'" + Echo(provider) + "/" + Echo(id) + "' isn't a session ref (<provider>/<id>)");

    /// <summary>The '?provider=' filter, or null when absent.</summary>
    /// <param name="provider">The query value.</param>
    /// <returns>The agent, or SND-CLI-003.</returns>
    internal static Result<AgentId?> Provider(string? provider)
    {
        if (provider is null)
        {
            return (AgentId?)null;
        }

        return AgentId.TryFrom(provider, out var agent) ? agent : SindriErrors.InvalidInput.With("'" + Echo(provider) + "' isn't an agent id");
    }

    /// <summary>The '?state=' filter, or null when absent.</summary>
    /// <param name="state">The query value.</param>
    /// <returns>The state, or SND-CLI-003.</returns>
    internal static Result<SessionState?> State(string? state) => state switch
    {
        null => (SessionState?)null,
        "unknown" => SessionState.Unknown,
        "running" => SessionState.Running,
        "asking" => SessionState.Asking,
        "idle" => SessionState.Idle,
        _ => SindriErrors.InvalidInput.With("'" + Echo(state) + "' isn't a session state (unknown, running, asking, idle)"),
    };

    /// <summary>The '?inWorkflow=' filter, or null when absent.</summary>
    /// <param name="inWorkflow">The query value.</param>
    /// <returns>The flag, or SND-CLI-003.</returns>
    internal static Result<bool?> InWorkflow(string? inWorkflow) => inWorkflow switch
    {
        null => (bool?)null,
        "true" => true,
        "false" => false,
        _ => SindriErrors.InvalidInput.With("inWorkflow is true or false"),
    };

    private static string Echo(string value) => SafeText.Line(SafeText.Escape(value), 80);
}
