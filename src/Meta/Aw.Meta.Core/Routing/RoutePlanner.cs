using System.Globalization;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Core.Routing;

/// <summary>Decides which lanes should move to another provider.</summary>
public static class RoutePlanner
{
    /// <summary>Plans the switches.</summary>
    /// <param name="routing">The routing config.</param>
    /// <param name="lanes">Every subject's standing; the meta is read for evidence and never moved.</param>
    /// <returns>The suggestions and the skipped lanes.</returns>
    public static RoutePlan Plan(RoutingConfig routing, IReadOnlyList<LaneStanding> lanes)
    {
        ArgumentNullException.ThrowIfNull(routing);
        ArgumentNullException.ThrowIfNull(lanes);
        var suggestions = new List<RouteSuggestion>();
        var skipped = new List<RouteSkip>();
        foreach (var lane in lanes.Where(static l => !l.IsMeta))
        {
            if (Why(routing, lane) is not { } reason)
            {
                continue;
            }

            var (move, cannot) = Move(routing, lanes, lane, reason, MetaAgent(lanes));
            if (move is not null)
            {
                suggestions.Add(move);
            }
            else if (cannot is not null)
            {
                skipped.Add(new RouteSkip(lane.Lane, cannot));
            }
        }

        return new RoutePlan(suggestions, skipped);
    }

    // Why the lane is running out; null when it has room.
    private static string? Why(RoutingConfig routing, LaneStanding lane)
    {
        if (lane.Limited && routing.SwitchWhenLimited)
        {
            return lane.State == UsageState.Limited
                ? $"limited: {lane.Kind?.ToString().ToLowerInvariant()} limit, resets {Resets(lane.ResetsAt)}"
                : $"limited: {lane.RunningAgent} reports its limit reached";
        }

        return routing.MinRemainingPct > 0 && lane.RemainingPct is { } left && left < routing.MinRemainingPct
            ? $"only {Percent(left)}% left in the fullest window (under {routing.MinRemainingPct.ToString(CultureInfo.InvariantCulture)}%)"
            : null;
    }

    // A RouteSuggestion, or the reason it cannot be made.
    private static (RouteSuggestion? Move, string? Cannot) Move(RoutingConfig routing, IReadOnlyList<LaneStanding> all, LaneStanding lane, string reason, string metaAgent)
    {
        if (lane.PendingRelaunch)
        {
            return (null, $"already assigned to {lane.Agent}; relaunch the lane to start it there");
        }

        var (target, cannot) = Target(routing, lane, metaAgent);
        if (target is not { } to)
        {
            return (null, cannot);
        }

        var others = all.Where(l => !l.IsMeta && !l.PendingRelaunch && string.Equals(l.Agent, to.Agent, StringComparison.Ordinal)).ToList();
        if (others.Find(l => RunningOut(routing, l)) is { } busy)
        {
            return (null, $"{to.Agent} is running out too (lane {busy.Lane})");
        }

        var known = others.Exists(static l => l.Signal is not null || l.State != UsageState.Unknown);
        return (new RouteSuggestion(lane.Lane, lane.Agent, to.From, to.Agent, to.Model, known ? reason : $"{reason}; {to.Agent} usage unknown", known), null);
    }

    // The provider and model of equal performance, or why there is none.
    private static ((string From, string Agent, string Model)? To, string? Cannot) Target(RoutingConfig routing, LaneStanding lane, string metaAgent)
    {
        if (lane.Model is not { } model)
        {
            return (null, $"model unknown: set routing.lanes.{lane.Lane}.model or routing.defaultModel");
        }

        if (routing.EquivalentOf(model) is not { } target)
        {
            return (null, $"no equivalent for {model} in routing.equivalents");
        }

        if (routing.AgentOf(target) is not { } agent)
        {
            return (null, $"{target} is not listed under routing.models");
        }

        if (string.Equals(agent, lane.Agent, StringComparison.Ordinal))
        {
            return (null, $"{target} is on the same provider as {model}");
        }

        // A Claude lane is messaged with SendMessage, which only a Claude meta has; lane-send refuses a Claude lane.
        return string.Equals(agent, LaneAgent.ClaudeCode, StringComparison.Ordinal) && !string.Equals(metaAgent, LaneAgent.ClaudeCode, StringComparison.Ordinal)
            ? (null, $"the meta can't message a Claude lane (the meta runs {metaAgent})")
            : ((model, agent, target), null);
    }

    // The meta's agent; claude-code when the list has no meta standing.
    private static string MetaAgent(IReadOnlyList<LaneStanding> all) => all.FirstOrDefault(static l => l.IsMeta)?.Agent ?? LaneAgent.ClaudeCode;

    private static bool RunningOut(RoutingConfig routing, LaneStanding lane) =>
        lane.Limited || (routing.MinRemainingPct > 0 && lane.RemainingPct is { } left && left < routing.MinRemainingPct);

    private static string Resets(DateTimeOffset? at) => at is { } when ? when.UtcDateTime.ToString("u", CultureInfo.InvariantCulture) : "unknown";

    private static string Percent(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
