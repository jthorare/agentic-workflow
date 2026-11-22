using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Coordination;

/// <summary>A non-Claude lane a message is delivered to: its team, name, record and agent facts.</summary>
/// <param name="Team">The team.</param>
/// <param name="Lane">The lane.</param>
/// <param name="Record">The lane record the launcher and the session-id hook left.</param>
/// <param name="Agent">The lane's agent.</param>
public sealed record LaneTarget(string Team, string Lane, LaneRecord Record, LaneAgent Agent)
{
    /// <summary>The lane's session and tmux name, "team-lane".</summary>
    public string Session => Team + "-" + Lane;
}
