using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The Home read port over the views reader (home spec §8): rows folded from events come back paged in rank order.</summary>
public sealed class SqliteHomeViewTests
{
    private readonly ViewsHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task List_reads_the_folded_rows_in_position_order()
    {
        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row("gh:pr:b#2", position: 1), []));
        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row("gh:pr:a#1", position: 0), []));
        var view = new SqliteHomeView(_h.Views);

        var page = (await view.List(new HomeQuery(Group: null, [HomeState.Open], After: null, Limit: 0), Token)).Value;

        Expect.Sequence(["gh:pr:a#1", "gh:pr:b#2"], page.Rows.Select(static r => r.Id.Value));
        Assert.Equal(2, page.Counts.NeedsYou);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Get_reads_one_row_and_answers_null_for_an_unknown_id()
    {
        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row("gh:pr:a#1"), []));
        var view = new SqliteHomeView(_h.Views);

        var known = (await view.Get(HomeRowId.From("gh:pr:a#1"), Token)).Value;
        var unknown = (await view.Get(HomeRowId.From("gh:pr:zz#9"), Token)).Value;

        Assert.Equal("gh:pr:a#1", known?.Id.Value);
        Assert.Null(unknown);
    }
}
