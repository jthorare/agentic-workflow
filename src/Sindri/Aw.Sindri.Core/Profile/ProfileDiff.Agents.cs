using System.Globalization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The paths of the 'agents:' keys the profile sets (screens spec E-28, Agents page spec §8).</summary>
public static partial class ProfileDiff
{
    private static IEnumerable<(string Key, string Value)> AgentKeys(AgentSettings? agents) =>
        AgentReservedKeys(agents).Concat(AgentTargetKeys(agents));

    private static IEnumerable<(string Key, string Value)> AgentReservedKeys(AgentSettings? agents)
    {
        var culture = CultureInfo.InvariantCulture;
        if (agents?.SoftLimit is { } soft)
        {
            yield return ("agents.softLimit", soft.ToString(culture));
        }

        if (agents?.IdleOwedMinutes is { } idleOwed)
        {
            yield return ("agents.idleOwedMinutes", idleOwed.ToString(culture));
        }

        if (agents?.SlotWaitMinutes is { } slotWait)
        {
            yield return ("agents.slotWaitMinutes", slotWait.ToString(culture));
        }

        if (agents?.Page is { } page)
        {
            yield return ("agents.page", page);
        }
    }

    private static IEnumerable<(string Key, string Value)> AgentTargetKeys(AgentSettings? agents)
    {
        var culture = CultureInfo.InvariantCulture;
        foreach (var (id, limits) in agents?.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal))
        {
            if (limits.DailyBudget is { } budget)
            {
                yield return ($"agents.{id}.dailyBudget", budget.ToString(culture));
            }

            if (limits.StepArgvHash is { } hashes)
            {
                yield return ($"agents.{id}.stepArgvHash", string.Join(", ", hashes));
            }
        }
    }
}
