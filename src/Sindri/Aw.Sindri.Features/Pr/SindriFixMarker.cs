using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// The marker 'sindri.fix:&lt;item id&gt;' Sindri puts on the thread it posts for a review-page fix task (review page spec §8.2). A thread that carries one is work
/// for the babysitter's comment loop even though Sindri wrote it; a marker that only looks like it ('sindri.fix:' and junk, 'sindri.fix-sent') is not.
/// The id is an item id ('ITM-' and 32 lowercase hex digits).
/// </summary>
internal static partial class SindriFixMarker
{
    private const string Prefix = "sindri.fix:";

    /// <summary>The item id the thread's markers name, or null when none is a fix marker.</summary>
    /// <param name="markers">The thread's markers.</param>
    /// <returns>The item id text.</returns>
    internal static string? Item(IEnumerable<string?> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        foreach (var marker in markers)
        {
            if (marker is not null && Shape.IsMatch(marker))
            {
                return marker[Prefix.Length..];
            }
        }

        return null;
    }

    [GeneratedRegex("^sindri\\.fix:(ITM-[0-9a-f]{32})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
