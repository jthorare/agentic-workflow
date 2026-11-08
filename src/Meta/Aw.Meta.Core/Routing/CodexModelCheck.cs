using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Routing;

/// <summary>Finds codex model names in the routing block that Codex does not list, so a typo is seen before a launch fails on it.</summary>
public static class CodexModelCheck
{
    /// <summary>The agent id whose models are checked.</summary>
    public const string Codex = "codex";

    /// <summary>Checks the names.</summary>
    /// <param name="routing">The routing config.</param>
    /// <param name="known">The slugs Codex lists.</param>
    /// <returns>One warning per unknown name, in the order found.</returns>
    public static IReadOnlyList<string> Warnings(RoutingConfig routing, IReadOnlySet<string> known)
    {
        ArgumentNullException.ThrowIfNull(routing);
        ArgumentNullException.ThrowIfNull(known);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        foreach (var (where, model) in Names(routing))
        {
            if (!known.Contains(model) && seen.Add(model))
            {
                warnings.Add($"warning: {where} names codex model '{model}', which ~/.codex/models_cache.json does not list: codex -m would fail on it");
            }
        }

        return warnings;
    }

    private static IEnumerable<(string Where, string Model)> Names(RoutingConfig routing)
    {
        if (routing.Models.TryGetValue(Codex, out var listed))
        {
            foreach (var model in listed)
            {
                yield return ("routing.models.codex", model);
            }
        }

        foreach (var (lane, route) in routing.Lanes)
        {
            if (route.Model is { } model && (string.Equals(route.Agent, Codex, StringComparison.Ordinal) || (route.Agent is null && string.Equals(routing.AgentOf(model), Codex, StringComparison.Ordinal))))
            {
                yield return ($"routing.lanes.{lane}", model);
            }
        }
    }
}
