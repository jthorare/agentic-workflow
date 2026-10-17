namespace Aw.Sindri.Core.Profile;

/// <summary>Which settings changed between two profiles, as key paths, for 'profile approve' to show before it records anything.</summary>
public static class ProfileDiff
{
    /// <summary>The key paths whose values differ; every path when there is no earlier approval.</summary>
    /// <param name="approved">The latest approved profile, if any.</param>
    /// <param name="live">The live profile.</param>
    /// <returns>The changed key paths, sorted.</returns>
    public static IReadOnlyList<string> Between(SindriProfile? approved, SindriProfile live)
    {
        var before = approved is null ? new Dictionary<string, string>(StringComparer.Ordinal) : Flatten(approved);
        var after = Flatten(live);
        return [.. before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Where(k => !before.TryGetValue(k, out var b) || !after.TryGetValue(k, out var a) || !string.Equals(a, b, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    private static Dictionary<string, string> Flatten(SindriProfile p)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = p.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["user"] = p.User,
            ["tenant"] = p.Tenant,
            ["api.port"] = (p.Api ?? new ApiSettings()).Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var repo in p.Repos ?? [])
        {
            values[$"repos.{repo.Name}"] = repo.Path;
        }

        foreach (var (name, settings) in p.Services ?? SindriProfile.NoServices)
        {
            values[$"services.{name}.every"] = settings.Every;
        }

        foreach (var (key, value) in Workflow(p.Workflow))
        {
            values[key] = value;
        }

        foreach (var (key, value) in LaunchAndAgents(p))
        {
            values[key] = value;
        }

        return values;
    }

    /// <summary>The 'launch:' and 'agents:' keys the profile sets; an absent key has no path, so adding one shows as a change.</summary>
    private static IEnumerable<(string Key, string Value)> LaunchAndAgents(SindriProfile p)
    {
        foreach (var entry in LaunchKeys(p.Launch))
        {
            yield return entry;
        }

        foreach (var entry in AgentKeys(p.Agents))
        {
            yield return entry;
        }
    }

    private static IEnumerable<(string Key, string Value)> LaunchKeys(LaunchSettings? launch)
    {
        if (launch?.Default is { } target)
        {
            yield return ("launch.default", target.Value);
        }

        if (launch?.ConfirmWindow is { } window)
        {
            yield return ("launch.confirmWindow", window);
        }

        foreach (var (repo, settings) in launch?.Repos ?? new Dictionary<string, LaunchRepoSettings>(StringComparer.Ordinal))
        {
            if (settings.Default is { } repoTarget)
            {
                yield return ($"launch.repos.{repo}.default", repoTarget.Value);
            }
        }

    }

    private static IEnumerable<(string Key, string Value)> AgentKeys(AgentSettings? agents)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (agents?.SoftLimit is { } soft)
        {
            yield return ("agents.softLimit", soft.ToString(culture));
        }

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

    /// <summary>The 'workflow:' keys the profile sets (S1-D11): an absent key has no path, so adding one shows as a change.</summary>
    private static IEnumerable<(string Key, string Value)> Workflow(WorkflowSettings? workflow)
    {
        (string Key, string? Value)[] keys =
        [
            ("workflow.deskTimeout", workflow?.DeskTimeout),
            ("workflow.eventHold", workflow?.EventHold),
            ("workflow.effects", workflow?.Effects is { } effects ? string.Join(", ", effects) : null),
            ("workflow.dispatchDelay", workflow?.DispatchDelay),
        ];
        return keys.Where(static k => k.Value is not null).Select(static k => (k.Key, k.Value!));   // why: the nulls are filtered out just before
    }
}
