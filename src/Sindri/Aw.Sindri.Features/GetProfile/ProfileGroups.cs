using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>
/// The wording of the profile preview's allowance, connections and human-only groups (setup-once spec §5.2 "Preview"): what the live profile
/// adds or widens against the approved one, and every explicit setting when nothing is approved. Pure; a key the profile leaves out says nothing.
/// Each profile key has one small method that says its sentence or nothing; a group only lists them.
/// </summary>
internal static class ProfileGroups
{
    private const string Desk = "desk";

    /// <summary>One work limit of the 'workflow:' block: how to read it, its default when the key is left out, and the sentence for a value.</summary>
    private sealed record LimitSpec(Func<WorkflowSettings, int?> Get, int Default, Func<int, string> Say);

    private static readonly LimitSpec[] _limits =
    [
        new(static w => w.Line?.Parallel, 2, static n => Number($"Sindri may run up to {n} phases at once")),
        new(static w => w.Line?.MaxOpenPrs, 4, static n => Number($"Sindri may keep up to {n} pull requests open")),
        new(static w => w.Line?.MaxStack, 4, static n => Number($"Sindri may stack phases up to {n} deep")),
        new(static w => w.Pr?.AutofixCap, 5, static n => Number($"Sindri may push up to {n} CI fixes for one pull request")),
    ];

    /// <summary>The spending and work limits the live profile raises or adds.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="live">The live profile.</param>
    /// <returns>One sentence per limit, in a fixed order.</returns>
    public static IEnumerable<string> Allowance(SindriProfile? approved, SindriProfile live) =>
        DailyBudgets(Targets(approved?.Agents), Targets(live.Agents))
            .Concat(_limits.Select(spec => Limit(approved?.Workflow, live.Workflow, approved is null, spec)).OfType<string>())
            .Concat(SizeBudgets(approved?.Workflow?.Plan, live.Workflow?.Plan, approved is null));

    /// <summary>The outside systems the live profile points Sindri at that the approved one did not.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="live">The live profile.</param>
    /// <returns>One sentence per connection, in a fixed order.</returns>
    public static IEnumerable<string> Connections(SindriProfile? approved, SindriProfile live) =>
        OnyxSentences(approved, live)
            .Concat(Compact(ForgeSentence(approved?.Workflow?.Forge, live.Workflow?.Forge)))
            .Concat(Providers(approved, live))
            .Concat(Compact(
                Changed(TargetOf(live), TargetOf(approved), static t => $"Workflow steps will launch the {t} agent"),
                Changed(LaunchOf(live), LaunchOf(approved), static l => $"Sindri will launch the {l} agent by default"),
                Newly(McpOf(live) == SetupSettings.Auto, McpOf(approved) == SetupSettings.Auto, "Sindri will register its MCP server in each repo on its own")));

    /// <summary>The steps the live profile keeps for you alone that the approved one did not.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="live">The live profile.</param>
    /// <returns>One sentence per stop, in a fixed order.</returns>
    public static IEnumerable<string> HumanOnly(SindriProfile? approved, SindriProfile live) =>
        Compact(
            Newly(RepliesOf(live) == Desk, RepliesOf(approved) == Desk, "You send every reply to a pull request comment yourself"),
            Newly(HeldBlocksMerge(live), HeldBlocksMerge(approved), "A held comment stops the merge until you clear it"),
            Newly(StartableOf(live) == RouteSettings.None, StartableOf(approved) == RouteSettings.None, "Routed items stop at the router until you start them"));

    private static IEnumerable<string> Compact(params string?[] sentences) => sentences.OfType<string>();

    // The sentence when the live value is set and differs from the approved one.
    private static string? Changed(string? now, string? was, Func<string, string> say) =>
        now is not null && !string.Equals(now, was, StringComparison.Ordinal) ? say(now) : null;

    private static string? Newly(bool now, bool was, string sentence) => now && !was ? sentence : null;

    private static string Number(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string? TargetOf(SindriProfile? profile) => profile?.Workflow?.Agent?.Target?.Value;

    private static string? LaunchOf(SindriProfile? profile) => profile?.Launch?.Default?.Value;

    private static string? McpOf(SindriProfile? profile) => profile?.Setup?.Mcp;

    private static string? RepliesOf(SindriProfile? profile) => profile?.Workflow?.Pr?.Replies;

    private static bool HeldBlocksMerge(SindriProfile? profile) => profile?.Workflow?.Pr?.HeldBlocksMerge == true;

    private static string? StartableOf(SindriProfile? profile) => profile?.Workflow?.Route?.Startable;

    // The sentence for a limit the live profile sets above the approved one (a missing key is its default); with nothing approved, any key it sets.
    private static string? Limit(WorkflowSettings? approved, WorkflowSettings? live, bool fresh, LimitSpec spec)
    {
        var was = approved is null ? null : spec.Get(approved);
        var now = live is null ? null : spec.Get(live);
        return now is { } value && (fresh || value > (was ?? spec.Default)) ? spec.Say(value) : null;
    }

    private static IReadOnlyDictionary<string, AgentLimits> Targets(AgentSettings? agents) =>
        agents?.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal);

    private static IEnumerable<string> DailyBudgets(IReadOnlyDictionary<string, AgentLimits> approved, IReadOnlyDictionary<string, AgentLimits> live) =>
        live.OrderBy(static t => t.Key, StringComparer.Ordinal)
            .Select(t => BudgetSentence(t.Key, t.Value.DailyBudget, approved.TryGetValue(t.Key, out var was) ? was.DailyBudget : null))
            .OfType<string>()
            .Concat(approved.Where(t => t.Value.DailyBudget is not null && !live.ContainsKey(t.Key)).Select(static t => t.Key).Order(StringComparer.Ordinal).Select(NoLimit));

    private static string? BudgetSentence(string agent, decimal? budget, decimal? was) => budget switch
    {
        { } value when was is null || value > was => Number($"{agent} may spend up to ${value:0.##} a day"),
        null when was is not null => NoLimit(agent),
        _ => null,
    };

    private static string NoLimit(string agent) => $"{agent} will have no daily spending limit";

    private static IEnumerable<string> SizeBudgets(PlanSettings? approved, PlanSettings? live, bool fresh) =>
        (live?.Sizes ?? new Dictionary<string, SizeSettings>(StringComparer.Ordinal))
            .OrderBy(static s => s.Key, StringComparer.Ordinal)
            .Select(s => SizeSentence(s.Key, s.Value, WasBudget(approved, s.Key), fresh))
            .OfType<string>();

    private static BudgetSettings? WasBudget(PlanSettings? approved, string size) =>
        approved?.Sizes is { } sizes && sizes.TryGetValue(size, out var old) ? old.Budget : null;

    private static string? SizeSentence(string size, SizeSettings settings, BudgetSettings? was, bool fresh)
    {
        var parts = Compact(
            SizePart("tokens", was?.Tokens, settings.Budget?.Tokens, fresh),
            SizePart("turns", was?.Turns, settings.Budget?.Turns, fresh),
            SizePart("minutes", was?.WallMinutes, settings.Budget?.WallMinutes, fresh)).ToList();
        return parts.Count > 0 ? $"A {size} phase may use up to {string.Join(", ", parts)}" : null;
    }

    private static string? SizePart(string unit, long? was, long? now, bool fresh) =>
        now is { } value && (fresh || was is null || value > was) ? Number($"{value} {unit}") : null;

    private static IEnumerable<string> OnyxSentences(SindriProfile? approved, SindriProfile live)
    {
        var now = Onyx(live);
        var was = Onyx(approved);
        return Compact(
            Changed(Url(now), Url(was), static url => $"Sindri will send what it reads to your Onyx at {url}"),
            Newly(Managed(now), Managed(was), "Sindri will start and look after Onyx on this machine"));
    }

    // 'context.sources.onyx' is the context engine's section: only its url and managed keys are read, and never the token.
    private static JsonElement? Onyx(SindriProfile? profile) => Child(Child(profile?.Context, "sources"), "onyx");

    private static JsonElement? Child(JsonElement? parent, string name) =>
        parent is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(name, out var child) ? child : null;

    private static string? Url(JsonElement? onyx) => Child(onyx, "url") is { ValueKind: JsonValueKind.String } url ? url.GetString() : null;

    private static bool Managed(JsonElement? onyx) => Child(onyx, "managed") is { ValueKind: JsonValueKind.True };

    private static string? ForgeSentence(ForgeSettings? approved, ForgeSettings? live)
    {
        if (live?.Adapter is not { } adapter || SameForge(approved, live, adapter))
        {
            return null;
        }

        return $"Sindri will use the {adapter} forge{AtHosts(live.Hosts ?? [])}, with {CredentialWords(live.Credential)}";
    }

    private static bool SameForge(ForgeSettings? approved, ForgeSettings live, string adapter) =>
        string.Equals(approved?.Adapter, adapter, StringComparison.Ordinal)
        && string.Equals(approved?.Credential, live.Credential, StringComparison.Ordinal)
        && (live.Hosts ?? []).All(h => approved?.Hosts?.Contains(h, StringComparer.Ordinal) == true);

    private static string AtHosts(IReadOnlyList<string> hosts) => hosts.Count == 0 ? string.Empty : $" at {string.Join(", ", hosts)}";

    // The source of the credential, never its value: a command is not echoed either.
    private static string CredentialWords(string? credential) => credential switch
    {
        null => "the token in AW_FORGE_TOKEN",
        _ when credential.StartsWith("env:", StringComparison.Ordinal) => $"the token in {credential["env:".Length..]}",
        _ => "a token from a command it runs",
    };

    private static IEnumerable<string> Providers(SindriProfile? approved, SindriProfile live)
    {
        var allowed = new HashSet<string>(approved is null ? [] : JudgeProviders.Effective(approved, repo: null, recipeLiteral: null), StringComparer.Ordinal);
        return BoxProviders(live, allowed)
            .Concat(RepoProviders(approved, live, allowed))
            .Where(static p => !string.Equals(p, JudgeProviderNames.Rules, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Select(static p => $"Judge steps may ask {p}");
    }

    private static IEnumerable<string> BoxProviders(SindriProfile live, HashSet<string> allowed) =>
        (live.Workflow?.Judge?.Providers ?? []).Where(p => !allowed.Contains(p));

    private static IEnumerable<string> RepoProviders(SindriProfile? approved, SindriProfile live, HashSet<string> allowed) =>
        (live.Repos ?? []).SelectMany(repo => (repo.Workflow?.Judge?.Providers ?? []).Where(p => !allowed.Contains(p) && !ApprovedRepoLists(approved, repo.Name, p)));

    private static bool ApprovedRepoLists(SindriProfile? approved, string repo, string provider) =>
        approved?.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo, StringComparison.Ordinal))?.Workflow?.Judge?.Providers?.Contains(provider, StringComparer.Ordinal) ?? false;
}
