using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// Which discovered sessions a scope lists (launcher.json 'sessions.scope'). The one decision: 'own' keeps a session whose tmux socket lies under this home's own tmux
/// directory, or whose launch id is a launch record of this home. A socket's name or kind says nothing: another home's 'sindri' socket has the same kind as ours.
/// </summary>
internal static class SessionScopeRules
{
    /// <summary>Whether the scope lists this session.</summary>
    /// <param name="scope">The configured scope.</param>
    /// <param name="host">The session's host, with its tmux socket path.</param>
    /// <param name="environment">The session's allowlisted environment.</param>
    /// <param name="ownTmuxDirectory">This home's tmux directory, or null.</param>
    /// <param name="ownLaunches">This home's launch records.</param>
    /// <returns>True when the session shows.</returns>
    internal static bool Lists(SessionScope scope, HostInfo host, IReadOnlyDictionary<string, string> environment, string? ownTmuxDirectory, IReadOnlySet<LaunchId> ownLaunches)
    {
        if (scope == SessionScope.All)
        {
            return true;
        }

        var onOwnSocket = host.Tmux?.Socket is { Length: > 0 } socket && ownTmuxDirectory is { Length: > 0 } directory && IsUnder(socket, directory);
        var launchedHere = environment.TryGetValue("AW_SINDRI_LAUNCH", out var text) && LaunchId.TryFrom(text, out var launch) && ownLaunches.Contains(launch);
        return onOwnSocket || launchedHere;
    }

    private static bool IsUnder(string path, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd('/') + "/";
        return Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal);
    }
}
