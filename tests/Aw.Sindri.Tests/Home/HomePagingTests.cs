using Aw.Sindri.Core.Home;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Home;

/// <summary>The one paging rule every Home reader shares (home spec §8).</summary>
public sealed class HomePagingTests
{
    private static readonly HomeState[] _open = [HomeState.Open];

    [Fact, Trait("Lane", "unit")]
    public void Rows_come_in_position_order_then_id_order()
    {
        HomeRow[] rows = [HomeRowSamples.Row("gh:pr:b#2", position: 1), HomeRowSamples.Row("gh:pr:a#1", position: 1), HomeRowSamples.Row("gh:pr:c#3", position: 0)];

        var page = HomePaging.Page(rows, new HomeQuery(Group: null, _open, After: null, Limit: 0));

        Assert.Equal(["gh:pr:c#3", "gh:pr:a#1", "gh:pr:b#2"], page.Rows.Select(static r => r.Id.Value));
        Assert.Null(page.Cursor);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cursor_names_the_last_row_and_the_next_page_starts_after_it()
    {
        HomeRow[] rows = [.. Enumerable.Range(0, 5).Select(static i => HomeRowSamples.Row($"gh:pr:r#{i}", position: i))];

        var first = HomePaging.Page(rows, new HomeQuery(Group: null, _open, After: null, Limit: 2));
        var second = HomePaging.Page(rows, new HomeQuery(Group: null, _open, first.Cursor, Limit: 2));
        var last = HomePaging.Page(rows, new HomeQuery(Group: null, _open, second.Cursor, Limit: 2));

        Assert.Equal(("gh:pr:r#1", "gh:pr:r#3", null), (first.Cursor, second.Cursor, last.Cursor));
        Assert.Equal(["gh:pr:r#4"], last.Rows.Select(static r => r.Id.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Counts_cover_the_whole_view_and_not_the_filter()
    {
        HomeRow[] rows =
        [
            HomeRowSamples.Row("gh:pr:a#1"),
            HomeRowSamples.Row("gh:pr:b#2", group: HomeGroup.Moving),
            HomeRowSamples.Row("gh:pr:c#3", state: HomeState.Snoozed),
            HomeRowSamples.Row("gh:pr:d#4", state: HomeState.Done),
        ];

        var page = HomePaging.Page(rows, new HomeQuery(HomeGroup.Moving, _open, After: null, Limit: 10));

        Assert.Equal(["gh:pr:b#2"], page.Rows.Select(static r => r.Id.Value));
        Assert.Equal(new HomeCounts(NeedsYou: 1, Moving: 1, Snoozed: 1, Done: 1), page.Counts);
    }
}
