namespace Aw.Sindri.Core.Home;

/// <summary>The one rule for a page of Home rows (home spec §8), shared by every <see cref="IHomeViewReader"/> so the real view and its fake page the same way.</summary>
public static class HomePaging
{
    /// <summary>The page size when a query asks for none (spec §8: the first page is 60 rows).</summary>
    public const int DefaultLimit = 60;

    /// <summary>The most rows a page holds.</summary>
    public const int MaxLimit = 200;

    /// <summary>
    /// Orders <paramref name="rows"/> by rank position and then id (ordinal), keeps those of the query's group and states, starts after the row named by its cursor
    /// (an unknown cursor starts at the top) and takes one page. The counts cover all of <paramref name="rows"/>.
    /// </summary>
    /// <param name="rows">Every row of the view.</param>
    /// <param name="query">What to read.</param>
    /// <returns>The page.</returns>
    public static HomePage Page(IReadOnlyList<HomeRow> rows, HomeQuery query)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(query);
        var counts = new HomeCounts(
            rows.Count(static r => r is { State: HomeState.Open, Group: HomeGroup.NeedsYou }),
            rows.Count(static r => r is { State: HomeState.Open, Group: HomeGroup.Moving }),
            rows.Count(static r => r.State == HomeState.Snoozed),
            rows.Count(static r => r.State == HomeState.Done));
        var ordered = rows
            .Where(r => (query.Group is null || r.Group == query.Group) && query.States.Contains(r.State))
            .OrderBy(static r => r.Rank.Position)
            .ThenBy(static r => r.Id.Value, StringComparer.Ordinal)
            .ToList();
        var start = query.After is null ? 0 : ordered.FindIndex(r => string.Equals(r.Id.Value, query.After, StringComparison.Ordinal)) + 1;
        var limit = query.Limit <= 0 ? DefaultLimit : Math.Min(query.Limit, MaxLimit);
        var page = ordered.Skip(start).Take(limit).ToList();
        var more = start + page.Count < ordered.Count;
        return new HomePage(page, counts, more ? page[^1].Id.Value : null);
    }
}
