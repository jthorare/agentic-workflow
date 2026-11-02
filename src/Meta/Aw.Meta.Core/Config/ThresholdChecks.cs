using System.Globalization;

namespace Aw.Meta.Core.Config;

/// <summary>The range checks for the numeric thresholds in the team config, applied when it is loaded.</summary>
public static class ThresholdChecks
{
    /// <summary>Finds the first threshold outside its range.</summary>
    /// <param name="team">The loaded config.</param>
    /// <returns>What is wrong, or null when every threshold is in range.</returns>
    public static string? Problem(TeamConfig team)
    {
        ArgumentNullException.ThrowIfNull(team);
        var (resources, builders, heavy) = (team.Resources, team.Builders, team.Heavy);
        return resources.DiskFloorGb < 0 ? Message("resources.diskFloorGb", "0 or more", resources.DiskFloorGb)
            : resources.MemFloorGb < 0 ? Message("resources.memFloorGb", "0 or more", resources.MemFloorGb)
            : resources.SwapHighPct is < 1 or > 100 ? Message("resources.swapHighPct", "1 to 100", resources.SwapHighPct)
            : builders.MaxPerLane is < 1 ? Message("builders.maxPerLane", "1 or more", builders.MaxPerLane.GetValueOrDefault())
            : builders.WorktreeGb < 0 ? Message("builders.worktreeGb", "0 or more", builders.WorktreeGb)
            : team.Context.RecycleAt < 1 ? Message("context.recycleAt", "1 or more", team.Context.RecycleAt)
            : team.Usage.ResumeMarginSeconds < 0 ? Message("usage.resumeMarginSeconds", "0 or more", team.Usage.ResumeMarginSeconds)
            : team.Usage.ProbeMinutes < 1 ? Message("usage.probeMinutes", "1 or more", team.Usage.ProbeMinutes)
            : heavy.Cap < 1 ? Message("heavy.cap", "1 or more", heavy.Cap)
            : AgentProblem(team.Agent);
    }

    private static string? AgentProblem(string? agent) =>
        agent is null || IsAgentId(agent) ? null : $"agent must be an agent id (^[a-z][a-z0-9-]{{1,31}}$), not '{agent}'";

    private static bool IsAgentId(string agent)
    {
        if (agent.Length is < 2 or > 32 || agent[0] is < 'a' or > 'z')
        {
            return false;
        }

        for (var i = 1; i < agent.Length; i++)
        {
            if (agent[i] is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static string Message(string key, string range, int value) =>
        string.Create(CultureInfo.InvariantCulture, $"{key} must be {range}, not {value}");
}
