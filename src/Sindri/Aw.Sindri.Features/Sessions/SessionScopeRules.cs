using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>Which discovered sessions a scope lists (launcher.json 'sessions.scope'). The one decision: 'own' keeps a session on Sindri's own tmux socket or one Sindri launched.</summary>
internal static class SessionScopeRules
{
    /// <summary>Whether the scope lists this session.</summary>
    /// <param name="scope">The configured scope.</param>
    /// <param name="host">The session's host, with its tmux socket kind.</param>
    /// <param name="environment">The session's allowlisted environment.</param>
    /// <returns>True when the session shows.</returns>
    internal static bool Lists(SessionScope scope, HostInfo host, IReadOnlyDictionary<string, string> environment)
    {
        if (scope == SessionScope.All)
        {
            return true;
        }

        var ownSocket = string.Equals(host.Tmux?.SocketKind, "sindri", StringComparison.Ordinal);   // the same rule the pane locator uses for "own"
        var launched = environment.TryGetValue("AW_SINDRI_LAUNCH", out var launch) && LaunchId.TryFrom(launch, out _);
        return ownSocket || launched;
    }
}
