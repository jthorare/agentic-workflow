namespace Aw.Meta.Core.Coordination;

/// <summary>Lane names as events carry them.</summary>
public static class LaneNames
{
    /// <summary>Strips the "&lt;team&gt;-" prefix from a session name ("t1-api" is lane "api").</summary>
    /// <param name="sender">The session name.</param>
    /// <param name="team">The team name.</param>
    /// <returns>The lane name; the sender unchanged when it has no such prefix.</returns>
    public static string WithoutTeam(string sender, string team)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(team);
        var prefix = team + "-";
        return sender.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? sender[prefix.Length..] : sender;
    }
}
