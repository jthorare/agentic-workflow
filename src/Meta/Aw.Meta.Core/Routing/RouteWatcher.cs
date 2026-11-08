using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Routing;

/// <summary>Prints a ROUTE SUGGEST line when a suggestion appears or changes, once, and keeps what it printed.</summary>
/// <param name="values">Keeps what was announced.</param>
public sealed class RouteWatcher(IKeyValueStore values)
{
    /// <summary>Takes one look.</summary>
    /// <param name="team">The team.</param>
    /// <param name="standings">Every subject's standing.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lines to print; none for a team with no equivalents.</returns>
    public async ValueTask<IReadOnlyList<string>> Check(TeamConfig team, IReadOnlyList<LaneStanding> standings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        if (team.Routing.Equivalents.Count == 0)
        {
            return [];
        }

        var plan = RoutePlanner.Plan(team.Routing, standings);
        var stored = await values.Get(KvKeys.RouteWatch, cancellationToken).ConfigureAwait(false);
        var before = (stored.IsSuccess ? stored.Value : null)?.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal) ?? [];
        var now = plan.Suggestions.Select(static s => s.Key).Order(StringComparer.Ordinal).ToList();

        // Advisory: if the memory can't be kept, a re-armed watch announces the suggestion again.
        if (!before.SetEquals(now))
        {
            _ = await values.Set(KvKeys.RouteWatch, string.Join('\n', now), cancellationToken).ConfigureAwait(false);
        }

        return [.. plan.Suggestions.Where(s => !before.Contains(s.Key)).Select(Line)];
    }

    private static string Line(RouteSuggestion s) =>
        $"ROUTE SUGGEST {s.Lane} {s.FromAgent}/{s.FromModel} -> {s.ToAgent}/{s.ToModel}: {s.Reason}";
}
