using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The whole repo above 60 nodes is one tile per area, largest first, with the name whole and the counts (#3674).</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapAreaTilesTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    private static WholeArea Area(string key, string label, int code, int features, int records, int decisions = 0) =>
        new(key, label, "trusted", code + features + records + decisions, new Dictionary<string, int> { ["code"] = code, ["feature"] = features, ["record"] = records, ["decision"] = decisions }, Unassigned: false);

    private static WholeMapView Repo(int nodes, params WholeArea[] areas)
    {
        var draft = WholeMapLayoutTests.Draft("areas-over-budget");
        return draft with { Counts = draft.Counts with { Nodes = nodes }, Areas = areas };
    }

    private static WholeMapViewModel Shown(WholeMapView view)
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(view);
        return map;
    }

    private static readonly WholeArea[] _three =
    [
        Area("feature:billing", "Billing", code: 12, features: 2, records: 1),
        Area("feature:control", "Control plane and authentication", code: 1980, features: 71, records: 59, decisions: 37),
        Area("feature:tabs", "Tab groups", code: 1, features: 1, records: 0),
    ];

    [Theory]
    [InlineData(60, false)]
    [InlineData(61, true)]
    [InlineData(12200, true)]
    public void More_than_sixty_nodes_is_area_tiles_and_sixty_or_fewer_is_not(int nodes, bool tiles)
    {
        var map = Shown(Repo(nodes, _three));

        Assert.Equal(tiles, map.ShowAreaTiles);
        Assert.Equal(!tiles, map.ShowStageTools);
    }

    [Fact]
    public void The_tiles_run_largest_first_with_the_name_whole_and_the_counts()
    {
        var map = Shown(Repo(200, _three));

        Assert.Equal(["Control plane and authentication", "Billing", "Tab groups"], map.AreaTiles.Select(t => t.Label));
        Assert.Equal("1980 code files · 71 features · 96 records", map.AreaTiles[0].Counts);   // why: the records are the records and the decisions
        Assert.Equal("1 code file · 1 feature · 0 records", map.AreaTiles[2].Counts);
        Assert.Equal("Billing, 12 code files · 2 features · 1 record. Press Enter to open.", map.AreaTiles[1].Name);
    }

    [Fact]
    public void A_tiles_bar_is_its_share_of_what_the_areas_hold_and_the_line_over_them_says_how_many()
    {
        var map = Shown(Repo(200, _three));

        Assert.Equal(1.0, map.AreaTiles.Sum(t => t.Share), precision: 6);
        Assert.Equal("3 areas · 2164 items", map.AreaTilesHead);
    }

    [Fact]
    public void A_tile_opens_its_area_through_the_page_and_the_tiles_go_while_it_is_open()
    {
        var map = Shown(Repo(200, _three));
        var opened = new List<string>();
        map.AreaOpenRequested += (_, e) => opened.Add(e.Value);

        map.OpenTile("feature:billing");

        Assert.Equal(["feature:billing"], opened);

        var area = WholeMapLayoutTests.Draft("area-open");
        map.ShowArea(area);

        Assert.False(map.ShowAreaTiles);
        Assert.Empty(map.AreaTiles);

        map.CloseArea();

        Assert.True(map.ShowAreaTiles);
    }

    [Fact]
    public void A_concept_is_never_the_overview_tiles()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.EnterConcept("Billing", "concept:billing");
        map.Show(Repo(200, _three));

        Assert.False(map.ShowAreaTiles);
    }

    private static List<MapSearchHit> Hits(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new MapSearchHit(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"code:src/F{i:000}.cs"), "code", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"F{i:000}.cs"), "feature:billing"))];

    [Fact]
    public void A_search_on_the_tiles_is_the_ranked_list_fifty_a_page_with_the_area_lists_pager()
    {
        var map = Shown(Repo(200, _three));

        map.ShowSearchList(Hits(120));

        Assert.Equal((false, true, true), (map.ShowAreaTiles, map.ShowAreaList, map.IsSearchList));
        Assert.Equal(50, map.Items.Count);
        Assert.Equal("Page 1 of 3 · 50 of 120 items", map.AreaPageText);

        map.NextPage();
        map.NextPage();

        Assert.Equal(20, map.Items.Count);
        Assert.Equal("F100.cs", map.Items[0].Label);
        Assert.False(map.CanNextPage);
    }

    [Fact]
    public void Ending_the_search_goes_back_to_the_tiles_and_a_search_with_no_match_keeps_them_with_the_note()
    {
        var map = Shown(Repo(200, _three));
        map.ShowSearchList(Hits(3));

        map.EndSearch();

        Assert.Equal((true, false, false, 1), (map.ShowAreaTiles, map.ShowAreaList, map.SearchActive, map.AreaPage));

        map.ShowSearchList([]);

        Assert.Equal((true, true), (map.ShowAreaTiles, map.NoMatches));
    }

    [Fact]
    public void A_click_on_a_match_picks_that_node()
    {
        var map = Shown(Repo(200, _three));
        map.ShowSearchList(Hits(3));
        var picked = new List<string>();
        map.NodeChosen += (_, e) => picked.Add(e.Value);

        map.ChooseItem(map.Items[1]);

        Assert.Equal(["code:src/F001.cs"], picked);
    }
}
