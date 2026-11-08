using System.Globalization;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Routing;
using Aw.Meta.Features.Routing;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Usage;

namespace Aw.Meta.Tests.Routing;

/// <summary>The usage world (team t1, lanes meta, api, ui; the clock is 05:00 UTC on 2026-10-15) with a routing config: Claude offers opus, sonnet and haiku, Codex astra and luna.</summary>
internal sealed class RoutingWorld
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 15, 5, 0, 0, TimeSpan.Zero);

    internal UsageWorld U { get; } = new();

    internal static RoutingConfig Cfg(int minRemainingPct = 10, bool switchWhenLimited = true, string? defaultModel = "opus") => new(
        models: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["claude-code"] = ["opus", "sonnet", "haiku"],
            ["codex"] = ["astra", "luna"],
        },
        equivalents: new Dictionary<string, string>(StringComparer.Ordinal) { ["opus"] = "astra", ["haiku"] = "luna" },
        defaultModel: defaultModel,
        switchWhenLimited: switchWhenLimited,
        minRemainingPct: minRemainingPct);

    /// <summary>A Codex rollout line carrying the rate limits.</summary>
    internal static string Rollout(double primaryUsed, double secondaryUsed, DateTimeOffset? primaryResets = null, DateTimeOffset? secondaryResets = null, string reached = "null")
    {
        var p = (primaryResets ?? Now.AddHours(2)).ToUnixTimeSeconds();
        var s = (secondaryResets ?? Now.AddDays(3)).ToUnixTimeSeconds();
        var text = """{"timestamp":"2026-10-15T04:50:00.000Z","type":"event_msg","payload":{"type":"token_count","info":null,"rate_limits":{"limit_id":"codex","limit_name":null,"primary":{"used_percent":PU,"window_minutes":300,"resets_at":PR},"secondary":{"used_percent":SU,"window_minutes":10080,"resets_at":SR},"credits":null,"plan_type":"plus","rate_limit_reached_type":RE}}}""";
        return text
            .Replace("PU", primaryUsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("SU", secondaryUsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("PR", p.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("SR", s.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("RE", reached, StringComparison.Ordinal);
    }

    internal async Task Ready(RoutingConfig? routing = null, string? agent = null)
    {
        await U.Ready();
        _ = await U.World.Config.Save(new TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta", agent: agent) { Routing = routing ?? Cfg() }, CancellationToken.None);
    }

    /// <summary>Makes a lane a Codex lane with a rollout.</summary>
    internal void CodexLane(string lane, string? rollout)
    {
        var session = "codex-" + lane;
        U.World.LaneFiles.Put(new LaneRecord(lane, SessionId.From(session), new LaneAgent("codex", "codex", Hooks: true), A.At(1)));
        if (rollout is not null)
        {
            U.World.CodexRollouts.Put(session, rollout + "\n", A.At(55));
        }
    }

    internal static LaneStanding Standing(string lane, string agent, string? model, bool limited = false, double? usedPct = null, string? runningAgent = null, bool reached = false) =>
        new(
            lane,
            agent,
            runningAgent ?? agent,
            model,
            limited ? Core.Usage.UsageState.Limited : Core.Usage.UsageState.Ok,
            limited ? Aw.Meta.Core.Usage.UsageKind.Session : null,
            limited ? Now.AddMinutes(30) : null,
            usedPct is { } used ? new ProviderSignal([new ProviderWindow("5h", used, Now.AddHours(1))], reached) : (reached ? new ProviderSignal([], LimitReached: true) : null),
            Note: null);

    internal RouteService Service() => new(U.World.Config, U.Reader, U.Standings);
}
