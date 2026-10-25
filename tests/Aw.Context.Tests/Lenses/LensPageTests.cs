using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

public sealed class LensPageTests
{
    private const string Anchor = "feature:tabs.tab-manager";
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);

    private static LensesView Whole()
    {
        var map = Maps.Checked(Maps.Parse(MapTabsRepo.MapJson));
        var graph = OverrideApplier.Apply(map.Graph, map.Overrides).Graph;
        return LensesBuilder.Build(new LensBasis("v13@7c3e1a2", Draft: false, _asOf), graph, NodeKey.From(Anchor), depth: 2).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_is_one_window_of_every_list_and_the_counts_stay_whole()
    {
        var view = Whole();
        var paged = LensPages.Window(view, limit: 1, offset: 0);

        Assert.Equal(view.Edges[0], paged.Edges.Single());
        Assert.Equal(view.Bands.Product.Counts, paged.Bands.Product.Counts);
        Assert.Equal(view.Coverage, paged.Coverage);
        Assert.Equal((0, 1, true), (paged.Page!.Offset, paged.Page.Limit, paged.Page.More));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_next_offset_starts_where_the_previous_page_stopped()
    {
        var view = Whole();
        var first = LensPages.Window(view, limit: 1, offset: 0);
        var second = LensPages.Window(view, limit: 1, offset: 1);

        Assert.Equal(view.Edges[1], second.Edges.Single());
        Assert.NotEqual(first.Edges[0], second.Edges[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_offset_past_the_end_is_an_empty_page_with_nothing_after_it()
    {
        var view = Whole();
        var page = LensPages.Window(view, limit: 1, offset: view.Edges.Count + 100);

        Assert.Empty(page.Edges);
        Assert.Empty(page.Bands.Product.Own);
        Assert.False(page.Page!.More);
    }
}
