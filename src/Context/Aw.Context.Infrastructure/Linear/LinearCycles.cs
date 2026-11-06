using Aw.Context.Core.Linear;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Which cycles the picker offers (spec 2026-11-04 §4.2): the current one, the next one and the last three.</summary>
internal static class LinearCycles
{
    private const int Past = 3;

    /// <summary>Picks the cycles from a team's list.</summary>
    /// <param name="cycles">Every cycle Linear listed.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The last three that ended, the current one and the next one that starts, oldest first.</returns>
    public static List<LinearLookupCycle> Pick(IEnumerable<LinearLookupCycle> cycles, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cycles);
        var all = cycles.OrderBy(static c => c.StartsAt).ThenBy(static c => c.Number).ToList();
        var past = all.Where(c => c.EndsAt <= now).TakeLast(Past);
        var current = all.Where(c => c.StartsAt <= now && now < c.EndsAt);
        var next = all.Where(c => c.StartsAt > now).Take(1);
        return [.. past.Concat(current).Concat(next)];
    }
}
