using System.Globalization;

namespace Aw.Meta.Core.Merge;

/// <summary>The pull requests handed to GitHub's merge queue, kept as "pr=ticks=head" triples separated by commas.</summary>
public static class MergeQueued
{
    /// <summary>Reads the stored value; malformed triples are ignored.</summary>
    /// <param name="stored">The stored value, or null.</param>
    /// <returns>The queued merge per pull request number.</returns>
    public static IReadOnlyDictionary<int, QueuedMerge> Parse(string? stored)
    {
        var queued = new Dictionary<int, QueuedMerge>();
        foreach (var triple in (stored ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = triple.Split('=');
            if (parts.Length == 3
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pr)
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks <= DateTimeOffset.MaxValue.UtcTicks)
            {
                queued[pr] = new QueuedMerge(pr, new DateTimeOffset(ticks, TimeSpan.Zero), parts[2]);
            }
        }

        return queued;
    }

    /// <summary>The stored value with one more queued merge (it replaces an earlier entry of the same pull request).</summary>
    /// <param name="stored">The stored value, or null.</param>
    /// <param name="entry">The queued merge.</param>
    /// <returns>The new value.</returns>
    public static string With(string? stored, QueuedMerge entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Format(new Dictionary<int, QueuedMerge>(Parse(stored)) { [entry.Pr] = entry });
    }

    /// <summary>The stored value without one pull request.</summary>
    /// <param name="stored">The stored value, or null.</param>
    /// <param name="pr">The pull request.</param>
    /// <returns>The new value; empty when none is left.</returns>
    public static string Without(string? stored, int pr) =>
        Format(Parse(stored).Where(entry => entry.Key != pr).ToDictionary(static entry => entry.Key, static entry => entry.Value));

    private static string Format(IReadOnlyDictionary<int, QueuedMerge> queued) =>
        string.Join(',', queued.OrderBy(static e => e.Key).Select(static e => string.Create(CultureInfo.InvariantCulture, $"{e.Key}={e.Value.At.UtcTicks}={e.Value.Head}")));
}
