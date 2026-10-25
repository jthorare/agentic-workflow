using System.Globalization;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The counters of a scope's memory.events file (cgroup v2); readable only while the scope is alive.</summary>
/// <param name="High">How often the session was throttled at MemoryHigh.</param>
/// <param name="Max">How often it hit MemoryMax.</param>
/// <param name="Oom">How often an allocation failed at the limit.</param>
/// <param name="OomKill">How many processes the kernel killed.</param>
internal sealed record MemoryEvents(long High, long Max, long Oom, long OomKill)
{
    /// <summary>Gets the counters of a scope with nothing recorded.</summary>
    internal static MemoryEvents None { get; } = new(0, 0, 0, 0);

    /// <summary>Reads the file's text ('key value' per line); a missing key counts 0 and a line that is not a number is skipped.</summary>
    /// <param name="text">The file's text, or null.</param>
    /// <returns>The counters.</returns>
    internal static MemoryEvents Parse(string? text)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts is [var key, var number] && long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                counts[key] = value;
            }
        }

        return new MemoryEvents(counts.GetValueOrDefault("high"), counts.GetValueOrDefault("max"), counts.GetValueOrDefault("oom"), counts.GetValueOrDefault("oom_kill"));
    }
}
