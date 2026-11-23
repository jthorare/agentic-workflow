namespace Aw.Sindri.Core.Home;

/// <summary>
/// The one rule every Home collector writes by (home spec §3.1, §3.2): compare what the source says now with the rows Home holds for that source.
/// A wanted row Home lacks is raised; a held row the source no longer has is cleared (whatever its state, so a clear ends a Done or a snooze too);
/// a row on both sides changes only when its why chip, waits-on, actions or links moved, never for its age, so a quiet source writes nothing.
/// </summary>
public static class HomeReconcile
{
    /// <summary>The plan for one pass.</summary>
    /// <param name="wanted">The rows the source has now, one per id.</param>
    /// <param name="held">The rows Home holds for the same source and kinds, in any state.</param>
    /// <returns>The plan; empty when nothing moved.</returns>
    public static HomePlan Plan(IReadOnlyList<HomeRaise> wanted, IReadOnlyList<HomeRow> held)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(held);
        var heldById = new Dictionary<string, HomeRow>(StringComparer.Ordinal);
        foreach (var row in held)
        {
            heldById[row.Id.Value] = row;
        }

        var wantedIds = new HashSet<string>(StringComparer.Ordinal);
        var raised = new List<HomeRaise>();
        var changed = new List<HomeRaise>();
        foreach (var want in wanted)
        {
            if (!wantedIds.Add(want.Row.Id.Value))
            {
                continue;   // why: a source that lists one object twice still makes one row
            }

            if (!heldById.TryGetValue(want.Row.Id.Value, out var current))
            {
                raised.Add(want);
            }
            else if (Moved(current, want.Row))
            {
                changed.Add(want with { Row = Next(current, want.Row) });
            }
        }

        var cleared = held.Where(r => !wantedIds.Contains(r.Id.Value)).DistinctBy(static r => r.Id.Value, StringComparer.Ordinal).Select(static r => r.Id).ToList();
        return new HomePlan(raised, changed, cleared);
    }

    private static HomeRow Next(HomeRow current, HomeRow want) => current with
    {
        Version = current.Version + 1,
        Title = want.Title,
        Facts = want.Facts,
        WaitsOn = want.WaitsOn,
        Links = want.Links,
        Actions = want.Actions,
        Source = want.Source,
    };

    private static bool Moved(HomeRow current, HomeRow next) =>
        !string.Equals(Why(current), Why(next), StringComparison.Ordinal)
        || !string.Equals(current.WaitsOn, next.WaitsOn, StringComparison.Ordinal)
        || !current.Actions.SequenceEqual(next.Actions, StringComparer.Ordinal)
        || !current.Links.SequenceEqual(next.Links);

    private static string? Why(HomeRow row) => row.Facts.FirstOrDefault(static f => f.Kind == HomeFactKind.Why)?.Text;
}
