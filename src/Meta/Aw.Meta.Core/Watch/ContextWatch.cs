using System.Globalization;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Watch;

/// <summary>Turns each poll's lane context sizes into one CONTEXT HIGH line per crossing and one CONTEXT OK line after a recycle.</summary>
public static class ContextWatch
{
    /// <summary>Reads the kept form.</summary>
    /// <param name="text">The stored text (lane names, comma-separated), or null when none was kept.</param>
    /// <returns>The lanes reported high.</returns>
    public static IReadOnlySet<string> Parse(string? text) =>
        text is null ? new HashSet<string>(StringComparer.Ordinal) : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);

    /// <summary>The kept form: the lanes reported high, comma-separated.</summary>
    /// <param name="high">The lanes.</param>
    /// <returns>The text.</returns>
    public static string Format(IReadOnlySet<string> high)
    {
        ArgumentNullException.ThrowIfNull(high);
        return string.Join(',', high.Order(StringComparer.Ordinal));
    }

    /// <summary>Compares a poll with the lanes already reported high. A lane whose size can't be told keeps its state.</summary>
    /// <param name="high">The lanes reported high so far.</param>
    /// <param name="now">This poll's lane contexts.</param>
    /// <param name="metaLane">The meta lane's name: it reports META RECYCLE instead of CONTEXT HIGH; null for none.</param>
    /// <returns>The lines to report and the lanes now reported high.</returns>
    public static (IReadOnlyList<string> Lines, IReadOnlySet<string> High) Step(IReadOnlySet<string> high, IReadOnlyList<LaneContext> now, string? metaLane = null)
    {
        ArgumentNullException.ThrowIfNull(high);
        ArgumentNullException.ThrowIfNull(now);
        var kept = new HashSet<string>(high, StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var lane in now)
        {
            if (lane.Verdict == ContextVerdict.Recycle && kept.Add(lane.Lane))
            {
                lines.Add(metaLane is not null && string.Equals(lane.Lane, metaLane, StringComparison.OrdinalIgnoreCase)
                    ? string.Create(CultureInfo.InvariantCulture, $"META RECYCLE {lane.Tokens / 1000}k")
                    : string.Create(CultureInfo.InvariantCulture, $"CONTEXT HIGH {lane.Lane} {lane.Tokens / 1000}k"));
            }
            else if (lane.Verdict == ContextVerdict.Ok && kept.Remove(lane.Lane))
            {
                lines.Add($"CONTEXT OK {lane.Lane}");
            }
        }

        return (lines, kept);
    }
}
