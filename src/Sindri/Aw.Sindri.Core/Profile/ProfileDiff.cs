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
        foreach (var (name, settings) in p.Services ?? SindriProfile.NoServices)
        {
            values[$"services.{name}.every"] = settings.Every;
        }

        foreach (var (key, value) in Repos(p.Repos).Concat(Workflow(p.Workflow)))
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

    private static IEnumerable<(string Key, string Value)> Repos(IReadOnlyList<RepoEntry>? repos)
    {
        foreach (var repo in repos ?? [])
        {
            yield return ($"repos.{repo.Name}", repo.Path);
            if (repo.Workflow?.Judge?.Providers is { } providers)
            {
                yield return ($"repos.{repo.Name}.workflow.judge.providers", string.Join(", ", providers));
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
            ("workflow.effects", Joined(workflow?.Effects)),
            ("workflow.dispatchDelay", workflow?.DispatchDelay),
        ];
        return keys.Concat(Runtimes(workflow)).Concat(Forge(workflow?.Forge)).Concat(LineKeys(workflow)).Where(static k => k.Value is not null).Select(static k => (k.Key, k.Value!));   // why: the nulls are filtered out just before
    }

    /// <summary>The line's keys (line spec LN-WF9): every one is a re-approval, so each key the profile sets has a path.</summary>
    private static IEnumerable<(string Key, string? Value)> LineKeys(WorkflowSettings? workflow) =>
        PackDefaults(workflow?.Packs).Concat(PackMaps(workflow?.Packs)).Concat(LineLimits(workflow?.Line)).Concat(PlanKeys(workflow?.Plan)).Concat(BuildKeys(workflow)).Concat(ModelKeys(workflow)).Concat(StepKeys(workflow));

    private static (string Key, string? Value)[] PackDefaults(PackSettings? packs) =>
    [
        ("workflow.packs.style.default", packs?.Style?.Default),
        ("workflow.packs.lenses.design.default", packs?.Lenses?.Design?.Default),
        ("workflow.packs.lenses.review.default", packs?.Lenses?.Review?.Default),
    ];

    private static IEnumerable<(string Key, string? Value)> PackMaps(PackSettings? packs) =>
        Mapped("workflow.packs.style.repos", packs?.Style?.Repos).Concat(Mapped("workflow.packs.lenses.design.repos", packs?.Lenses?.Design?.Repos))
            .Concat(Mapped("workflow.packs.lenses.review.repos", packs?.Lenses?.Review?.Repos)).Concat(Mapped("workflow.packs.pins", packs?.Pins));

    private static IEnumerable<(string Key, string? Value)> Mapped(string path, IReadOnlyDictionary<string, string>? values) =>
        (values ?? new Dictionary<string, string>(StringComparer.Ordinal)).Select(v => ($"{path}.{v.Key}", (string?)v.Value));

    private static (string Key, string? Value)[] LineLimits(LineSettings? line) =>
    [
        ("workflow.line.parallel", Number(line?.Parallel)),
        ("workflow.line.maxOpenPrs", Number(line?.MaxOpenPrs)),
        ("workflow.line.maxStack", Number(line?.MaxStack)),
    ];

    private static IEnumerable<(string Key, string? Value)> PlanKeys(PlanSettings? plan) =>
        new (string Key, string? Value)[] { ("workflow.plan.maxPhaseSize", plan?.MaxPhaseSize) }.Concat((plan?.Sizes ?? new Dictionary<string, SizeSettings>(StringComparer.Ordinal)).SelectMany(static s => SizeKeys(s.Key, s.Value)));

    private static (string Key, string? Value)[] SizeKeys(string size, SizeSettings limits) =>
    [
        ($"workflow.plan.sizes.{size}.files", Number(limits.Files)),
        ($"workflow.plan.sizes.{size}.items", Number(limits.Items)),
        ($"workflow.plan.sizes.{size}.budget.tokens", Number(limits.Budget?.Tokens)),
        ($"workflow.plan.sizes.{size}.budget.turns", Number(limits.Budget?.Turns)),
        ($"workflow.plan.sizes.{size}.budget.wallMinutes", Number(limits.Budget?.WallMinutes)),
    ];

    private static (string Key, string? Value)[] BuildKeys(WorkflowSettings? workflow) =>
    [
        ("workflow.prove.holdoutMin", Number(workflow?.Prove?.HoldoutMin)),
        ("workflow.build.commands", Joined(workflow?.Build?.Commands)),
        ("workflow.ship.draft", workflow?.Ship?.Draft?.ToString()),
    ];

    private static (string Key, string? Value)[] ModelKeys(WorkflowSettings? workflow) =>
    [
        ("workflow.design.models.writer", workflow?.Design?.Models?.Writer),
        ("workflow.design.models.lens", workflow?.Design?.Models?.Lens),
        ("workflow.review.models.lens", workflow?.Review?.Models?.Lens),
    ];

    private static (string Key, string? Value)[] StepKeys(WorkflowSettings? workflow) =>
    [
        ("workflow.steps.longMax", workflow?.Steps?.LongMax),
        ("workflow.docs.globs", Joined(workflow?.Docs?.Globs)),
    ];

    private static string? Number(long? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? Joined(IReadOnlyList<string>? values) => values is null ? null : string.Join(", ", values);

    private static (string Key, string? Value)[] Runtimes(WorkflowSettings? workflow) =>
    [
        ("workflow.agent.target", workflow?.Agent?.Target?.Value),
        ("workflow.judge.providers", Joined(workflow?.Judge?.Providers)),
        ("workflow.route.startable", workflow?.Route?.Startable),
    ];

    private static (string Key, string? Value)[] Forge(ForgeSettings? forge) =>
    [
        ("workflow.forge.adapter", forge?.Adapter),
        ("workflow.forge.hosts", Joined(forge?.Hosts)),
        ("workflow.forge.credential", forge?.Credential),
    ];
}
