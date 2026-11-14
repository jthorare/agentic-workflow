using System.Globalization;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>One short line on what the sandbox refused in a launch (#3565), for a failure text that would otherwise hide it.</summary>
public static class DenialSummary
{
    private const int Shown = 3;

    /// <summary>The line: 'the sandbox refused N actions (target xCount, target, +K more)'.</summary>
    /// <param name="files">The launch's denial counters.</param>
    /// <returns>
    /// The line, or null when nothing was refused. N counts every refusal, overflow included. Entries group by target across files, most refused first, then by target; the top three show,
    /// ' xC' only when C is above 1. K is the distinct targets not shown plus one per overflow row (a kind whose targets the counter stopped naming).
    /// </returns>
    public static string? Line(IReadOnlyList<DenialFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var entries = files.SelectMany(file => file.Entries).GroupBy(entry => entry.Target, StringComparer.Ordinal)
            .Select(group => (Target: group.Key, Count: group.Sum(entry => entry.Count)))
            .OrderByDescending(group => group.Count).ThenBy(group => group.Target, StringComparer.Ordinal).ToList();
        var overflow = files.SelectMany(file => file.Overflow).ToList();
        var total = entries.Sum(entry => entry.Count) + overflow.Sum(row => row.Count);
        if (total == 0)
        {
            return null;
        }

        var parts = entries.Take(Shown).Select(entry => entry.Count > 1 ? string.Create(CultureInfo.InvariantCulture, $"{entry.Target} x{entry.Count}") : entry.Target).ToList();
        var more = Math.Max(0, entries.Count - Shown) + overflow.Count;
        if (more > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"+{more} more"));
        }

        var noun = total == 1 ? "action" : "actions";
        return string.Create(CultureInfo.InvariantCulture, $"the sandbox refused {total} {noun} ({string.Join(", ", parts)})");
    }
}
