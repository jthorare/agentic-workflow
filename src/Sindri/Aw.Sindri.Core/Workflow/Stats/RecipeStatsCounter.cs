using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// Counts a recipe's stats from its workflow ledger rows (API spec §3.8, E-24): a pure function of the rows, the window and the clock. A run is an
/// instance whose start row names the recipe; it counts when it started inside the window, and its later rows count wherever they fall. A park, an
/// escalation or a takeover begins when a row's suspension kind changes to it and ends when it changes again; a park answered or an escalation decided
/// is one that a human's answer ended, and a run with any of those or a takeover needed a person once, however many it had.
/// </summary>
public static class RecipeStatsCounter
{
    private const string Scheme = "sha256:";

    /// <summary>The stats of <paramref name="recipe"/> for the runs that started inside <paramref name="window"/> before <paramref name="now"/>.</summary>
    /// <param name="events">The workflow rows, in ledger order.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="hashPrefix">A recipe hash (in full or a prefix of 8 or more digits) to count only runs under it; null for every version.</param>
    /// <param name="window">The window.</param>
    /// <param name="now">The read's time.</param>
    /// <returns>The stats.</returns>
    public static RecipeStats Count(IReadOnlyList<StatEvent> events, RecipeId recipe, string? hashPrefix, StatsWindow window, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(window);
        var since = now - window.Span;
        var tally = new StatsTally();
        var walker = new StatsRunWalker(tally);
        var runs = new List<StatsRun>();
        foreach (var rows in events.GroupBy(static e => e.Address, StringComparer.Ordinal))
        {
            var start = rows.FirstOrDefault(static e => e.RecipeId is not null);
            if (start is null || start.RecipeId is not { } id || !id.Equals(recipe) || start.At < since || !InVersion(start.RecipeHash, hashPrefix))
            {
                continue;
            }

            runs.Add(walker.Walk(rows, start));
        }

        return StatsBuilder.Build(new StatsRequest(recipe, hashPrefix, window, now), runs, tally);
    }

    /// <summary>Whether any root instance (an address of three segments) of <paramref name="recipe"/> ever started.</summary>
    /// <param name="events">The workflow rows.</param>
    /// <param name="recipe">The recipe.</param>
    /// <returns>True when one did.</returns>
    public static bool HasRoot(IReadOnlyList<StatEvent> events, RecipeId recipe)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.Any(e => e.RecipeId is { } id && id.Equals(recipe) && e.Address.AsSpan().Count('/') == 2);
    }

    private static bool InVersion(string? hash, string? prefix)
    {
        if (prefix is null)
        {
            return true;
        }

        var wanted = prefix.StartsWith(Scheme, StringComparison.Ordinal) ? prefix[Scheme.Length..] : prefix;
        return RecipeHash.IsPrefix(prefix) && hash is not null && hash.StartsWith(Scheme, StringComparison.Ordinal) && hash[Scheme.Length..].StartsWith(wanted, StringComparison.Ordinal);
    }
}
