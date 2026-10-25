using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The whole map's view model (whole-map spec §3.1, §3.2): its head and notes, the list of features, the zoom thresholds. Goldens as in <see cref="WholeMapLayoutTests"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapViewModelTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    [Fact]
    public void Before_an_answer_there_is_nothing_ready_and_nothing_drawn()
    {
        var map = new WholeMapViewModel(() => _utc);

        Assert.Equal((false, false, false, string.Empty), (map.IsReady, map.IsLoading, map.IsFailed, map.Head));
        Assert.Null(map.Geometry);
    }

    [Fact]
    public void A_node_level_answer_reads_the_head_with_the_version_and_the_size_and_age()
    {
        var map = new WholeMapViewModel(() => _utc);

        map.Show(WholeMapLayoutTests.Draft("nodes-small"));

        Assert.Equal(("Context · the whole map · v1", false, false), (map.Head, map.ByArea, map.HasNote));
        Assert.Equal("14 items · 15 links · built 1 Jan 2026 00:00", map.SizeText);
        Assert.Equal((true, false, false), (map.IsReady, map.IsLoading, map.IsFailed));
    }

    [Fact]
    public void An_answer_by_area_has_no_grouping_note()
    {
        var map = new WholeMapViewModel(() => _utc);

        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));

        Assert.Equal((true, string.Empty, false), (map.ByArea, map.ByAreaNote, map.HasNote));
    }

    [Fact]
    public void The_list_of_features_follows_the_map_order_and_each_names_its_size()
    {
        var map = new WholeMapViewModel(() => _utc);
        var answer = WholeMapLayoutTests.Draft("nodes-small");

        map.Show(answer);

        Assert.Equal(map.Geometry!.Order, map.Areas.Select(a => a.Key), StringComparer.Ordinal);
        var tabs = map.Areas.Single(a => a.Key == "feature:tabs.tab-groups");
        Assert.Equal(("Tab groups", "6 items", "Tab groups, 6 items. Press Enter to open."), (tabs.Label, tabs.MembersText, tabs.Name));
        Assert.Equal("2 decisions, 1 action, 1 code file, 1 feature, 1 screen", tabs.KindsText);
    }

    [Fact]
    public void Opening_a_tile_on_the_overview_asks_the_page_to_read_that_feature()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));
        var asked = new List<string>();
        map.AreaOpenRequested += (_, e) => asked.Add(e.Value);

        map.Areas[1].Open();

        Assert.Equal([map.Areas[1].Key], asked);
    }

    [Fact]
    public void Opening_a_feature_on_a_node_level_map_moves_the_camera_to_its_column()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));
        var asked = new List<WholeRect>();
        map.ZoomRequested += (_, e) => asked.Add(e.Value);

        map.Areas[0].Open();

        Assert.Equal([map.Geometry!.Columns[0].Rect], asked);
    }

    [Fact]
    public void An_open_feature_draws_its_items_alone_with_the_way_back_and_back_restores_the_overview()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));
        var overview = map.Geometry;
        var opened = WholeMapLayoutTests.Draft("area-open");

        map.ShowArea(opened);

        Assert.Equal((true, "Tab groups", string.Empty), (map.IsAreaOpen, map.AreaTitle, map.ByAreaNote));
        Assert.Equal((false, 6, 1), (map.ByArea, map.Geometry!.Nodes.Count, map.Geometry.Columns.Count));
        map.CloseArea();
        Assert.Equal((false, true), (map.IsAreaOpen, ReferenceEquals(overview, map.Geometry)));
        Assert.Equal(string.Empty, map.ByAreaNote);
    }

    [Fact]
    public void A_large_feature_is_served_whole_so_there_is_no_cut_note()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));

        map.ShowArea(WholeMapLayoutTests.Draft("area-truncated"));

        Assert.Equal(string.Empty, map.ByAreaNote);   // why: the engine no longer cuts an opened area, so nothing is left to say is not shown
    }

    [Fact]
    public void A_node_click_is_raised_with_its_key()
    {
        var map = new WholeMapViewModel(() => _utc);
        var chosen = new List<string>();
        map.NodeChosen += (_, e) => chosen.Add(e.Value);

        map.ChooseNode("feature:tabs");

        Assert.Equal(["feature:tabs"], chosen);
    }

    [Fact]
    public void A_failed_read_keeps_what_was_drawn_and_a_new_answer_clears_the_failure()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));

        map.Loading();
        Assert.Equal((true, true, false), (map.IsReady, map.IsLoading, map.IsFailed));
        map.Failed();
        Assert.Equal((true, false, true), (map.IsReady, map.IsLoading, map.IsFailed));
        Assert.NotNull(map.Geometry);

        map.Show(WholeMapLayoutTests.Draft("nodes-small"));
        Assert.Equal((true, false, false), (map.IsReady, map.IsLoading, map.IsFailed));
    }

    [Theory]
    [InlineData(0.2, 0.25)]
    [InlineData(0.4, 0.4)]
    [InlineData(3.0, 1.0)]
    public void The_first_open_fits_the_map_but_never_below_the_floor(double fit, double expected) =>
        Assert.Equal(expected, WholeMapViewModel.FirstOpenScale(fit));

    [Theory]
    [InlineData(0.34, true)]
    [InlineData(0.35, false)]
    [InlineData(1.0, false)]
    public void Nodes_are_dots_below_about_a_third_of_the_size(double scale, bool dots) =>
        Assert.Equal(dots, WholeMapViewModel.UsesDots(scale));

    [Theory]
    [InlineData(0, 0, "Solid")]
    [InlineData(5, 5, "Solid")]
    [InlineData(6, 0, "Hatched")]
    [InlineData(12, 0, "Ringed")]
    [InlineData(18, 0, "Solid")]
    public void More_areas_than_colours_reuse_a_colour_with_the_next_pattern(int index, int colour, string pattern)
    {
        var (c, p) = WholePalette.For(index);

        Assert.Equal((colour, pattern), (c, p.ToString()));
    }

    [Fact]
    public void Zooming_marks_the_answers_nodes_asks_the_camera_for_their_box_and_unzooming_fits_the_world_again()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));
        var asked = new List<WholeRect>();
        var fits = 0;
        map.ZoomRequested += (_, e) => asked.Add(e.Value);
        map.FitRequested += (_, _) => fits++;
        var some = map.Geometry!.Nodes.Take(2).Select(n => n.Key).ToList();

        Assert.True(map.Zoom([.. some, "feature:not.on.the.map"]));

        Assert.Equal((true, true), (map.IsZoomed, map.HitKeys.SetEquals(some)));
        Assert.Equal(WholeZoomBox.Around([.. map.Geometry.Nodes.Where(n => some.Contains(n.Key)).Select(n => n.Rect)]), Assert.Single(asked));
        map.Unzoom();
        Assert.Equal((false, 0, 1), (map.IsZoomed, map.HitKeys.Count, fits));
    }

    [Fact]
    public void Nothing_on_the_map_moves_nothing()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));
        var moved = false;
        map.ZoomRequested += (_, _) => moved = true;

        Assert.False(map.Zoom(["feature:not.on.the.map"]));

        Assert.Equal((false, false), (moved, map.IsZoomed));
    }

    [Fact]
    public void Zooming_into_features_opens_them_beside_the_tiles_that_stay_and_counts_the_ones_left_out()
    {
        var map = new WholeMapViewModel(() => _utc);
        var overview = WholeMapLayoutTests.Draft("areas-over-budget");
        map.Show(overview);
        var opened = WholeMapLayoutTests.Draft("area-open");
        var key = opened.Nodes![0].Key;

        Assert.True(map.ZoomIntoAreas(overview, [opened], [key], notOpened: 2));

        Assert.Equal(("2 more features touched", true), (map.MoreTouchedText, map.HasMoreTouched));
        Assert.Equal(opened.Nodes.Count, map.Geometry!.Nodes.Count);
        Assert.Equal(overview.Areas.Count - 1, map.Geometry.Tiles.Count);
        map.Unzoom();
        Assert.Equal((true, 0, string.Empty), (map.Geometry!.ByArea, map.Geometry.Nodes.Count, map.MoreTouchedText));
    }
}
