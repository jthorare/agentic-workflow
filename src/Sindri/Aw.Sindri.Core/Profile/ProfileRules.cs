using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks the JSON shape can't express: version, required text, service names and cadences.</summary>
public static class ProfileRules
{
    /// <summary>Checks <paramref name="profile"/> and returns it with defaults applied (sorted services and repos).</summary>
    /// <param name="profile">The deserialized profile.</param>
    /// <returns>The canonical profile, or SND-PROFILE-002 naming the key path and the fix.</returns>
    public static Result<SindriProfile> Check(SindriProfile profile) => Check(profile, box: null);

    /// <summary>Checks <paramref name="profile"/> against what the box allows, and returns it with defaults applied.</summary>
    /// <param name="profile">The deserialized profile.</param>
    /// <param name="box">The box's limits (the heavy-slot cap), or null to check the profile alone.</param>
    /// <returns>The canonical profile, or SND-PROFILE-002 naming the key path and the fix.</returns>
    public static Result<SindriProfile> Check(SindriProfile profile, BoxLimits? box)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _ = box;   // why: the heavy-slot cap limited 'workflow.line.parallel', which went with the line; the parameter stays so callers keep their signature
        if (profile.SchemaVersion != SindriProfile.CurrentSchemaVersion)
        {
            return Invalid("schemaVersion", string.Create(CultureInfo.InvariantCulture,
                $"is {profile.SchemaVersion}; this build reads {SindriProfile.CurrentSchemaVersion}"));
        }

        if (string.IsNullOrWhiteSpace(profile.User) || string.IsNullOrWhiteSpace(profile.Tenant))
        {
            return Invalid(string.IsNullOrWhiteSpace(profile.User) ? "user" : "tenant", "must be non-empty text");
        }

        if (profile.Api is { Port: < 0 or (> 0 and < 1024) or > 65535 })
        {
            return Invalid("api.port", "must be 0 (a free port) or 1024 to 65535");
        }

        var invalid = Sections(profile);
        if (invalid is not null)
        {
            return invalid;
        }

        return Canonical(profile);
    }

    private static Error? Sections(SindriProfile profile) =>
        OnyxLeftover(profile.Context) ?? Services(profile.Services) ?? Workflow(profile) ?? ProfileGlobRules.Check(profile) ?? IndexRules.Check(profile) ?? LaunchKeys(profile.Launch) ?? AgentKeys(profile.Agents) ?? SetupKeys(profile.Setup) ?? SandboxRules.Check(profile.Sandbox) ?? Later(profile);

    private static Error? Later(SindriProfile profile) => PrReviewRules.Check(profile) ?? TodoObsidianRules.Check(profile);

    /// <summary>A 'context.sources.onyx' section left from before Onyx was removed is refused for one release, never ignored (owned-index spec Q15).</summary>
    private static Error? OnyxLeftover(JsonElement? context) =>
        context is { ValueKind: JsonValueKind.Object } c && c.TryGetProperty("sources", out var sources)
        && sources.ValueKind == JsonValueKind.Object && sources.TryGetProperty("onyx", out _)
            ? SindriErrors.IndexOnyxInProfile.With("context.sources.onyx: Sindri no longer runs or reads Onyx. Remove the 'onyx' entry under context.sources in profile.yaml. Edits apply on save; there is no approve step")
            : null;

    /// <summary>The 'setup:' block (setup-once spec §7.1): 'setup.mcp' is auto or off; the OAuth client ids (first-run spec decision 15a) are one word each.</summary>
    private static Error? SetupKeys(SetupSettings? setup) =>
        setup?.Mcp is { } mcp && mcp is not (SetupSettings.Auto or SetupSettings.Off)
            ? Invalid("setup.mcp", $"'{mcp}' is not auto or off")
            : OauthClientId("setup.oauth.github", setup?.Oauth?.Github) ?? OauthClientId("setup.oauth.linear", setup?.Oauth?.Linear);

    private static Error? OauthClientId(string keyPath, string? clientId) =>
        clientId is not null && (string.IsNullOrWhiteSpace(clientId) || clientId.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c)))
            ? Invalid(keyPath, "must be the OAuth app's client id: one word, no spaces")
            : null;

    /// <summary>The 'launch:' keys: the confirm window is a duration, and a repo has a name. Agent ids are checked by pattern when read, never against the catalog.</summary>
    private static Error? LaunchKeys(LaunchSettings? launch)
    {
        if (launch?.ConfirmWindow is { } window && !RecipeDuration.TryFrom(window, out _))
        {
            return Invalid("launch.confirmWindow", $"'{window}' is not a duration: a whole number and m, h or d (for example 90m, 2h)");
        }

        return (launch?.Repos ?? new Dictionary<string, LaunchRepoSettings>(StringComparer.Ordinal)).Keys.Any(string.IsNullOrWhiteSpace)
            ? Invalid("launch.repos", "a repo name must be non-empty text")
            : null;
    }

    private const int MaxHash = 128;

    private static Error? AgentProblem(string id, AgentLimits limits)
    {
        if (!Launch.AgentId.TryParse(id, provider: null, out _))
        {
            return Invalid($"agents.{id}", "is not an agent id: a lowercase letter, then lowercase letters, digits or '-', 2 to 32 characters");
        }

        if (limits.DailyBudget is <= 0m)
        {
            return Invalid($"agents.{id}.dailyBudget", "must be above 0 (a decimal amount in USD)");
        }

        var hashes = limits.StepArgvHash ?? [];
        for (var i = 0; i < hashes.Count; i++)
        {
            if (hashes[i].Length is 0 or > MaxHash || hashes[i].Any(char.IsWhiteSpace))
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"agents.{id}.stepArgvHash[{i}]"), $"must be 1 to {MaxHash} characters with no spaces");
            }
        }

        return null;
    }

    /// <summary>The 'agents:' keys: ids match the id pattern (never the catalog), 'softLimit' is a fraction, a budget is positive, a hash is a short word.</summary>
    private static Error? AgentKeys(AgentSettings? agents)
    {
        if (AgentPageKeys(agents) is { } pageProblem)
        {
            return pageProblem;
        }

        if (agents?.SoftLimit is <= 0m or > 1m)
        {
            return Invalid("agents.softLimit", "must be above 0 and at most 1 (a fraction of the daily budget)");
        }

        foreach (var (id, limits) in agents?.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal))
        {
            if (AgentProblem(id, limits) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    /// <summary>The Agents page's keys (Agents page spec §8): the two minute counts are whole numbers from 1 to 1440 and 'page' is 'rows' or 'legacy'; a bad one is SND-AGENT-010 with the key path.</summary>
    private static Error? AgentPageKeys(AgentSettings? agents) =>
        MinutesProblem(AgentSettings.IdleOwedMinutesKey, agents?.IdleOwedMinutes) ?? MinutesProblem(AgentSettings.SlotWaitMinutesKey, agents?.SlotWaitMinutes)
        ?? (agents?.Page is { } page && page is not (AgentSettings.PageRows or AgentSettings.PageLegacy)
            ? SindriErrors.AgentSettingInvalid.With($"agents.{AgentSettings.PageKey}: '{page}' is not rows or legacy (fix: set it to rows or legacy)")
            : null);

    private static Error? MinutesProblem(string key, int? minutes) =>
        minutes is < AgentSettings.MinMinutes or > AgentSettings.MaxMinutes
            ? SindriErrors.AgentSettingInvalid.With(string.Create(CultureInfo.InvariantCulture,
                $"agents.{key}: {minutes} is not between {AgentSettings.MinMinutes} and {AgentSettings.MaxMinutes} (fix: set a whole number of minutes in that range)"))
            : null;

    internal static string? GlobProblem(string glob) => ProfileGlobRules.GlobProblem(glob);

    private static SindriProfile Canonical(SindriProfile profile) => profile with
    {
        Repos = [.. (profile.Repos ?? []).OrderBy(r => r.Name, StringComparer.Ordinal)],
        Services = new SortedDictionary<string, ServiceSettings>(
            (profile.Services ?? SindriProfile.NoServices).ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal),
        Api = profile.Api ?? new ApiSettings(),
        Launch = profile.Launch is { Repos: { } repos } launch ? launch with { Repos = new SortedDictionary<string, LaunchRepoSettings>(repos.ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal) } : profile.Launch,
        Review = profile.Review is { Settings: { } named } review ? review with { Settings = new SortedDictionary<string, PrReviewSettingsEntry>(named.ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal) } : profile.Review,
        Agents = profile.Agents is { Targets: { } targets } agents ? agents with { Targets = new SortedDictionary<string, AgentLimits>(targets.ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal) } : profile.Agents,
    };

    /// <summary>Each service's name and cadence.</summary>
    private static Error? Services(IReadOnlyDictionary<string, ServiceSettings>? services)
    {
        foreach (var (name, settings) in services ?? SindriProfile.NoServices)
        {
            if (!ServiceName.TryFrom(name, out _))
            {
                return Invalid($"services.{name}", "is not a service name: kebab-case, 1 to 40 characters");
            }

            if (Cadence.Parse(settings.Every) is { IsSuccess: false } bad)
            {
                return Invalid($"services.{name}.every", bad.Error.Message);
            }
        }

        return null;
    }

    /// <summary>The 'workflow:' durations (S1-D11): each key that is set must be a duration such as '1m', '4h' or '2d'.</summary>
    private static Error? Durations(WorkflowSettings? workflow)
    {
        foreach (var (key, value) in new[] { ("deskTimeout", workflow?.DeskTimeout), ("eventHold", workflow?.EventHold) })
        {
            if (value is not null && !RecipeDuration.TryFrom(value, out _))
            {
                return Invalid($"workflow.{key}", $"'{value}' is not a duration: a whole number and m, h or d (for example 1m, 4h, 2d)");
            }
        }

        return null;
    }

    /// <summary>
    /// The effect keys (S1-D11, engine P6): each 'workflow.effects' entry is an 'effect.*' block id, and
    /// 'workflow.dispatchDelay' reads like a service cadence (seconds allowed, 5s to 24h).
    /// </summary>
    private static Error? Effects(WorkflowSettings? workflow)
    {
        var effects = workflow?.Effects ?? [];
        for (var i = 0; i < effects.Count; i++)
        {
            if (!EffectSettings.IsEffectId(effects[i]))
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"workflow.effects[{i}]"),
                    $"'{effects[i]}' is not an effect id: 'effect.' and a block name (for example effect.notify.desk)");
            }
        }

        return workflow?.DispatchDelay is { } delay && EffectSettings.Delay(delay) is null
            ? Invalid("workflow.dispatchDelay", $"'{delay}' is not a delay: a whole number and s, m or h (for example 10s), from 5s to 24h")
            : null;
    }

    private static Error? Workflow(SindriProfile profile) => DurationsAndAgent(profile.Workflow) ?? Effects(profile.Workflow) ?? Judges(profile) ?? ForgeRules.Check(profile.Workflow?.Forge) ?? PrRules.Check(profile.Workflow?.Pr, profile.Workflow?.Docs) ?? RouteAndPoll(profile.Workflow);

    private static Error? RouteAndPoll(WorkflowSettings? workflow) => Route(workflow?.Route) ?? Poll(workflow?.Poll);

    private static Error? DurationsAndAgent(WorkflowSettings? workflow) => Durations(workflow) ?? ModelKeyRules.Shape(workflow?.Agent) ?? ForEachRules.Check(workflow?.ForEach) ?? FlowRules.Check(workflow);

    /// <summary>The poll keys (#2258, PR 11): 'workflow.poll.intervalSeconds' is at least 30, 'workflow.poll.catchUpPages' is 1 to 100.</summary>
    private static Error? Poll(PollSettings? poll)
    {
        if (poll?.IntervalSeconds is < PollSettings.MinIntervalSeconds)
        {
            return Invalid("workflow.poll.intervalSeconds", string.Create(CultureInfo.InvariantCulture, $"{poll.IntervalSeconds} is below the least interval, {PollSettings.MinIntervalSeconds} seconds"));
        }

        return poll?.CatchUpPages is < 1 or > PollSettings.MaxCatchUpPages
            ? Invalid("workflow.poll.catchUpPages", string.Create(CultureInfo.InvariantCulture, $"{poll.CatchUpPages} is not between 1 and {PollSettings.MaxCatchUpPages}"))
            : null;
    }

    /// <summary>The router stub's key (engine P7 plan D8): 'workflow.route.startable' is all or none.</summary>
    private static Error? Route(RouteSettings? route) =>
        route?.Startable is { } startable && startable is not (RouteSettings.All or RouteSettings.None)
            ? Invalid("workflow.route.startable", $"'{startable}' is not all or none")
            : null;

    /// <summary>The judge provider lists (DT-J plan Q8): the box's and each repo's name only known providers.</summary>
    private static Error? Judges(SindriProfile profile)
    {
        foreach (var (path, names) in JudgeLists(profile))
        {
            var unknown = names.Select(static (name, i) => (name, i)).FirstOrDefault(static n => !JudgeProviders.Known.Contains(n.name, StringComparer.Ordinal));
            if (unknown.name is not null)
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{unknown.i}]"), $"'{unknown.name}' is not a judge provider: {string.Join(", ", JudgeProviders.Known)}");
            }
        }

        return JudgeWidths(profile);
    }

    /// <summary>The judge call width (issue 1440): the box's and each repo's 'parallel' is 1 to 16.</summary>
    private static Error? JudgeWidths(SindriProfile profile)
    {
        var (path, width) = JudgeWidthKeys(profile).FirstOrDefault(static w => w.Width is < JudgeParallel.Min or > JudgeParallel.Max);
        return path is null ? null : Invalid(path, string.Create(CultureInfo.InvariantCulture, $"{width} is not between {JudgeParallel.Min} and {JudgeParallel.Max}"));
    }

    private static IEnumerable<(string Path, int Width)> JudgeWidthKeys(SindriProfile profile)
    {
        if (profile.Workflow?.Judge?.Parallel is { } box)
        {
            yield return ("workflow.judge.parallel", box);
        }

        foreach (var repo in profile.Repos ?? [])
        {
            if (repo.Workflow?.Judge?.Parallel is { } own)
            {
                yield return ($"repos.{repo.Name}.workflow.judge.parallel", own);
            }
        }
    }

    private static IEnumerable<(string Path, IReadOnlyList<string> Names)> JudgeLists(SindriProfile profile)
    {
        if (profile.Workflow?.Judge?.Providers is { } box)
        {
            yield return ("workflow.judge.providers", box);
        }

        foreach (var repo in profile.Repos ?? [])
        {
            if (repo.Workflow?.Judge?.Providers is { } own)
            {
                yield return ($"repos.{repo.Name}.workflow.judge.providers", own);
            }
        }
    }

    private static Error Invalid(string keyPath, string problem) =>
        SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
