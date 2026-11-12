using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// Perf's three budget checks for a 5,000-row RowList (#2978), counted in row containers and not in wall clock, so a loaded box does not flake them (#2946):
/// a scroll from top to bottom never holds more than two screens of rows, the first frame builds no more than that, and the whole scroll makes only a few screens of containers.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowListBudgetTests
{
    private const int Groups = 5;
    private const int PerGroup = 1000;
    private const double WindowHeight = 800;
    private const int Visible = 26;   // 800 px / 32 px rows, plus the partly shown one

    // The specs' wall-clock budgets (Home first paint 100 ms, Messages 16 ms a frame) times this, so a loaded box does not flake them (#2946). The counts above stay the tight guard.
    private const double Loose = 5;

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowListBudgetTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task A_scroll_from_top_to_bottom_never_holds_more_than_two_screens_of_rows() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(Groups, PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        var total = (Groups * PerGroup * 32d) + (Groups * 28d);

        var worst = 0;
        for (var offset = 0d; offset <= total; offset += 320)
        {
            list.ScrollTo(offset);
            Settle(window);
            worst = Math.Max(worst, list.RealizedRowCount);
        }

        Assert.True(worst <= 2 * Visible, string.Create(CultureInfo.InvariantCulture, $"{worst} rows realized at once, budget {2 * Visible}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_first_frame_builds_no_more_than_two_screens_of_row_containers() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(Groups, PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };

        window.Show();   // no RunJobs and no UpdateLayout: what Show builds is the first frame

        Assert.True(list.ContainersCreated > 0, "the first frame builds rows; a list that builds none passes any upper bound");
        Assert.True(list.ContainersCreated <= 2 * Visible, string.Create(CultureInfo.InvariantCulture, $"{list.ContainersCreated} containers before the first frame, budget {2 * Visible}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_whole_scroll_makes_a_few_screens_of_containers_and_not_5000() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(Groups, PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        var total = (Groups * PerGroup * 32d) + (Groups * 28d);

        for (var offset = 0d; offset <= total; offset += 320)
        {
            list.ScrollTo(offset);
            Settle(window);
        }

        Assert.True(list.ContainersCreated <= 4 * Visible, string.Create(CultureInfo.InvariantCulture, $"{list.ContainersCreated} containers made over the scroll, budget {4 * Visible}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_first_paint_of_5000_rows_fits_Homes_100_ms_budget_with_room_for_a_loaded_box() => await OnUi(() =>
    {
        var clock = Stopwatch.StartNew();
        var list = new RowList { ItemsSource = RowGallery.Many(Groups, PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        clock.Stop();

        Assert.True(list.ContainersCreated > 0);
        Assert.True(clock.Elapsed.TotalMilliseconds <= 100 * Loose, string.Create(CultureInfo.InvariantCulture, $"first paint took {clock.Elapsed.TotalMilliseconds:0} ms, budget {100 * Loose:0} ms (100 ms x {Loose})"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Appending_10000_rows_one_by_one_costs_about_one_container_fill_per_visible_row_and_none_off_screen() => await OnUi(() =>
    {
        var group = new LiveGroup("Messages", Enumerable.Range(0, 50).Select(i => LiveRow.At(i)));
        var list = new RowList { ItemsSource = new[] { group }, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        var before = list.ContainersPrepared;

        var clock = Stopwatch.StartNew();
        for (var i = 50; i < 10_050; i++)
        {
            group.Rows.Add(LiveRow.At(i));
        }

        clock.Stop();
        Settle(window);

        var fills = list.ContainersPrepared - before;
        Assert.True(fills <= 2 * Visible, string.Create(CultureInfo.InvariantCulture, $"{fills} container fills for 10,000 appends below the fold, budget {2 * Visible}"));
        Assert.True(clock.Elapsed.TotalMilliseconds <= 5000, string.Create(CultureInfo.InvariantCulture, $"10,000 appends took {clock.Elapsed.TotalMilliseconds:0} ms (a rebuild per append is about 5x10^7 entry visits)"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task An_append_in_view_fills_one_container_and_not_the_whole_screen() => await OnUi(() =>
    {
        var group = new LiveGroup("Messages", Enumerable.Range(0, 5).Select(i => LiveRow.At(i)));
        var list = new RowList { ItemsSource = new[] { group }, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        var before = list.ContainersPrepared;

        for (var i = 5; i < 25; i++)
        {
            group.Rows.Add(LiveRow.At(i));
            Settle(window);
        }

        var fills = list.ContainersPrepared - before;
        Assert.True(fills <= 2 * 20, string.Create(CultureInfo.InvariantCulture, $"{fills} container fills for 20 appends, expected about 20"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Clearing_and_refilling_10000_rows_in_one_group_stays_inside_a_loose_bound() => await OnUi(() =>
    {
        var group = new LiveGroup("Messages", Enumerable.Range(0, 10_000).Select(i => LiveRow.At(i)));
        var list = new RowList { ItemsSource = new[] { group }, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);

        var clock = Stopwatch.StartNew();
        group.Rows.Clear();
        foreach (var i in Enumerable.Range(0, 10_000))
        {
            group.Rows.Add(LiveRow.At(i, "n", "New "));
        }

        clock.Stop();
        Settle(window);

        Assert.True(clock.Elapsed.TotalMilliseconds <= 8000, string.Create(CultureInfo.InvariantCulture, $"clear and refill of 10,000 rows took {clock.Elapsed.TotalMilliseconds:0} ms"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Swapping_in_a_new_list_of_10000_rows_is_one_rebuild_inside_a_loose_bound() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(1, 10_000), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);

        var clock = Stopwatch.StartNew();
        list.ItemsSource = RowGallery.Many(1, 10_000).Select(g => g).ToList();
        Settle(window);
        clock.Stop();

        Assert.True(clock.Elapsed.TotalMilliseconds <= 16 * 20 * Loose, string.Create(CultureInfo.InvariantCulture, $"a rebuild of 10,000 rows took {clock.Elapsed.TotalMilliseconds:0} ms"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Scrolling_10000_rows_costs_a_frame_budget_per_step_with_room_for_a_loaded_box() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(1, 10_000), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        var steps = 0;
        var clock = Stopwatch.StartNew();

        for (var offset = 0d; offset <= 10_000 * 32d; offset += 3200)
        {
            list.ScrollTo(offset);
            Settle(window);
            steps++;
        }

        clock.Stop();
        var perStep = clock.Elapsed.TotalMilliseconds / steps;
        Assert.True(perStep <= 16 * Loose, string.Create(CultureInfo.InvariantCulture, $"{perStep:0.0} ms a scroll step over {steps} steps, budget {16 * Loose:0} ms (16 ms x {Loose})"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_recycle_reuses_the_cells_and_glyph_geometry_and_builds_none_per_row_scrolled_past() => await OnUi(() =>
    {
        var cellsBefore = Row.CellsBuilt;
        var contentsBefore = RowFactViews.ContentsBuilt;
        var glyphBefore = RowGlyphView.Allocations;
        var list = new RowList { ItemsSource = RowGallery.Many(Groups, PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        var total = (Groups * PerGroup * 32d) + (Groups * 28d);

        for (var offset = 0d; offset <= total; offset += 320)
        {
            list.ScrollTo(offset);
            Settle(window);
        }

        var containers = list.ContainersCreated;
        var columns = RowGallery.Columns.Count;
        var cells = Row.CellsBuilt - cellsBefore;
        var contents = RowFactViews.ContentsBuilt - contentsBefore;
        var glyphs = RowGlyphView.Allocations - glyphBefore;
        Assert.True(cells <= containers * (columns + 1), string.Create(CultureInfo.InvariantCulture, $"{cells} cells built for {containers} containers; rows scrolled past would make thousands"));
        Assert.True(contents <= containers * columns, string.Create(CultureInfo.InvariantCulture, $"{contents} fact contents built for {containers} containers"));
        Assert.True(glyphs <= containers + 40, string.Create(CultureInfo.InvariantCulture, $"{glyphs} glyph geometries, pens and palettes built for {containers} containers"));
        window.Close();
        return true;
    });
}
