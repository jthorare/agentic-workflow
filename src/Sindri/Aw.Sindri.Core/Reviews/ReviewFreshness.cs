using System.Globalization;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Facts;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The words of the Reviews list's freshness line and its stale line (PR review page spec §6.3).</summary>
public static class ReviewFreshness
{
    /// <summary>One source's freshness row: failing with its code, still syncing, or synced some minutes ago.</summary>
    /// <param name="source">The source's state.</param>
    /// <param name="now">The clock's now.</param>
    /// <returns>The row.</returns>
    public static ProjectFreshnessRow Row(ProjectSourceFreshness source, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.LastCode is { } code
            ? new ProjectFreshnessRow(source.Source, ProjectFreshnessState.Failing, "failing: " + code, source.LastSync)
            : !source.Usable
                ? new ProjectFreshnessRow(source.Source, ProjectFreshnessState.Syncing, "still syncing", source.LastSync)
                : new ProjectFreshnessRow(source.Source, ProjectFreshnessState.Ok, Synced(source.LastSync, now), source.LastSync);
    }

    /// <summary>The line above the last rows when a source failed: "PR list is from 23:45: github failed: SND-INDEX-401".</summary>
    /// <param name="source">The source's state.</param>
    /// <returns>The line, or null when the source did not fail.</returns>
    public static ReviewStale? Stale(ProjectSourceFreshness source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.LastCode is not { } code)
        {
            return null;
        }

        var from = source.LastSync is { } at ? "PR list is from " + at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) : "PR list was never read";
        return new ReviewStale($"{from}: {source.Source} failed: {code}");
    }

    private static string Synced(DateTimeOffset? at, DateTimeOffset now)
    {
        if (at is null)
        {
            return "synced";
        }

        var minutes = (long)Math.Floor((now - at.Value).TotalMinutes);
        return minutes switch
        {
            < 1 => "synced just now",
            < 60 => string.Create(CultureInfo.InvariantCulture, $"synced {minutes} min ago"),
            _ => string.Create(CultureInfo.InvariantCulture, $"synced {minutes / 60} h ago"),
        };
    }
}
