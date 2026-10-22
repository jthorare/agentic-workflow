using System.Globalization;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Setup;

/// <summary>What a profile allows, in plain words: the pure read behind the profile step's preview. Names, paths, effect kinds, cadences and numbers only; no secret is in any of them.</summary>
public static class ProfilePreview
{
    /// <summary>The lines for <paramref name="profile"/>: its repos, its services, the effects its workflows may have and the agents' limits.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The lines, repos first.</returns>
    public static IReadOnlyList<SetupPreviewLine> Lines(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        List<SetupPreviewLine> lines = [];
        foreach (var repo in profile.Repos ?? [])
        {
            lines.Add(new SetupPreviewLine("repo", $"Works in {repo.Name} ({repo.Path})."));
        }

        foreach (var (name, settings) in (profile.Services ?? SindriProfile.NoServices).OrderBy(static s => s.Key, StringComparer.Ordinal))
        {
            lines.Add(new SetupPreviewLine("service", $"Runs the {name} service every {settings.Every}."));
        }

        var effects = profile.Workflow?.Effects ?? [];
        lines.Add(new SetupPreviewLine("allows", effects.Count == 0 ? "Workflows may not change anything outside Sindri." : "Workflows may: " + string.Join(", ", effects) + "."));
        AddLimits(lines, profile.Agents);
        return lines;
    }

    private static void AddLimits(List<SetupPreviewLine> lines, AgentSettings? agents)
    {
        if (agents is null)
        {
            return;
        }

        if (agents.SoftLimit is { } soft)
        {
            lines.Add(new SetupPreviewLine("limit", string.Create(CultureInfo.InvariantCulture, $"Warns you when an agent has used {soft * 100:0.##}% of its daily budget.")));
        }

        foreach (var (agent, limits) in (agents.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal)).OrderBy(static t => t.Key, StringComparer.Ordinal))
        {
            if (limits.DailyBudget is { } budget)
            {
                lines.Add(new SetupPreviewLine("limit", string.Create(CultureInfo.InvariantCulture, $"{agent} may spend up to {budget:0.##} a day.")));
            }
        }
    }
}
