using System.Globalization;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// The text kept under <see cref="KvKeys.RecycleGaveUp"/>: one "lane|time" line per lane whose recycle waiter gave up since the lane last
/// recycled (#4264). A give-up used to be only a log NOTE, so nobody saw the meta stop recycling.
/// </summary>
public static class RecycleGiveUps
{
    private const char Separator = '|';

    /// <summary>The stored text with the lane's give-up added, or its time moved to <paramref name="at"/>.</summary>
    /// <param name="stored">The stored text; null when the key was never set.</param>
    /// <param name="lane">The lane whose waiter gave up.</param>
    /// <param name="at">When it gave up.</param>
    /// <returns>The new text.</returns>
    public static string With(string? stored, string lane, DateTimeOffset at) =>
        string.Join('\n', Others(stored, lane).Append(lane + Separator + at.ToString("O", CultureInfo.InvariantCulture)));

    /// <summary>The stored text without the lane's give-up.</summary>
    /// <param name="stored">The stored text; null when the key was never set.</param>
    /// <param name="lane">The lane that recycled.</param>
    /// <returns>The new text; empty when no give-up is left.</returns>
    public static string Without(string? stored, string lane) => string.Join('\n', Others(stored, lane));

    /// <summary>The status line for the stored give-ups.</summary>
    /// <param name="stored">The stored text; null when the key was never set.</param>
    /// <returns>An "ATTENTION" line naming each lane and when its waiter gave up; null when none is stored.</returns>
    public static string? Line(string? stored)
    {
        var lanes = Lines(stored).Select(Describe).ToList();
        return lanes.Count == 0
            ? null
            : "ATTENTION recycle gave up: " + string.Join(", ", lanes) + "; no turn ended within 30 min and nothing was typed. Recycle by hand (aw-meta lane-recycle <lane> --digest <abs> --detach)";
    }

    private static string Describe(string line)
    {
        var parts = line.Split(Separator);
        return parts.Length == 2 && DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? parts[0] + " at " + at.UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture)
            : parts[0];
    }

    private static IEnumerable<string> Others(string? stored, string lane) =>
        Lines(stored).Where(l => !l.StartsWith(lane + Separator, StringComparison.Ordinal));

    private static string[] Lines(string? stored) =>
        (stored ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
