using System.Globalization;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Config;

/// <summary>The checks for the routing block of the team config, applied when it is loaded.</summary>
internal static class RoutingChecks
{
    /// <summary>Finds the first thing wrong with the routing block.</summary>
    /// <param name="team">The loaded config.</param>
    /// <returns>What is wrong, or null when the block is usable.</returns>
    internal static string? Problem(TeamConfig team)
    {
        var routing = team.Routing;
        return routing.MinRemainingPct is < 0 or > 100
            ? string.Create(CultureInfo.InvariantCulture, $"routing.minRemainingPct must be 0 to 100, not {routing.MinRemainingPct}")
            : ModelsProblem(routing) ?? EquivalentsProblem(routing) ?? DefaultModelProblem(routing) ?? LanesProblem(team);
    }

    private static string? ModelsProblem(RoutingConfig routing)
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (agent, models) in routing.Models)
        {
            if (!ThresholdChecks.IsAgentId(agent))
            {
                return $"routing.models key must be an agent id (^[a-z][a-z0-9-]{{1,31}}$), not '{agent}'";
            }

            foreach (var model in models)
            {
                if (string.IsNullOrWhiteSpace(model))
                {
                    return $"routing.models.{agent} has a blank model name";
                }

                if (!owners.TryAdd(model, agent))
                {
                    return $"routing.models: '{model}' is listed under both {owners[model]} and {agent}";
                }
            }
        }

        return null;
    }

    private static string? EquivalentsProblem(RoutingConfig routing)
    {
        foreach (var (model, other) in routing.Equivalents)
        {
            foreach (var name in new[] { model, other })
            {
                if (routing.AgentOf(name) is null)
                {
                    return $"routing.equivalents: '{name}' is not listed under routing.models";
                }
            }

            if (string.Equals(routing.AgentOf(model), routing.AgentOf(other), StringComparison.Ordinal))
            {
                return $"routing.equivalents: '{model}' and '{other}' are on the same provider";
            }
        }

        return null;
    }

    private static string? DefaultModelProblem(RoutingConfig routing) =>
        routing.DefaultModel is { } model && routing.AgentOf(model) is null
            ? $"routing.defaultModel '{model}' is not listed under routing.models"
            : null;

    private static string? LanesProblem(TeamConfig team)
    {
        foreach (var (lane, route) in team.Routing.Lanes)
        {
            if (string.Equals(lane, team.MetaLane, StringComparison.Ordinal))
            {
                return $"routing.lanes.{lane} is the meta lane: a team runs the meta on its own agent (team.json agent)";
            }

            if (!team.Lanes.Contains(lane, StringComparer.Ordinal))
            {
                return $"routing.lanes.{lane} is not a lane of the team";
            }

            var problem = RouteProblem(team.Routing, lane, route) ?? ClaudeLaneProblem(team, lane, route);
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    // A Claude lane is messaged with SendMessage, which only a Claude meta has.
    private static string? ClaudeLaneProblem(TeamConfig team, string lane, LaneRoute route)
    {
        var agent = route.Agent ?? (route.Model is { } model ? team.Routing.AgentOf(model) : null);
        return string.Equals(agent, LaneAgent.ClaudeCode, StringComparison.Ordinal) && team.Agent is { } meta && !string.Equals(meta, LaneAgent.ClaudeCode, StringComparison.Ordinal)
            ? $"routing.lanes.{lane}: a {meta} meta can't message a Claude lane (set team.json agent to claude-code, or route the lane elsewhere)"
            : null;
    }

    private static string? RouteProblem(RoutingConfig routing, string lane, LaneRoute route)
    {
        if (route.Agent is { } agent && !routing.Models.ContainsKey(agent))
        {
            return $"routing.lanes.{lane}.agent '{agent}' has no entry in routing.models";
        }

        if (route.Model is not { } model)
        {
            return null;
        }

        return routing.AgentOf(model) is not { } owner
            ? $"routing.lanes.{lane}.model '{model}' is not listed under routing.models"
            : route.Agent is { } named && !string.Equals(named, owner, StringComparison.Ordinal)
                ? $"routing.lanes.{lane}: '{model}' is not offered by {named}"
                : null;
    }
}
