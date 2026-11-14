using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// Perf's budget checks for a RowList (#2978). The first three are counted in row containers and not in wall clock, so a loaded box does not flake them (#2946): a scroll from top to bottom never holds more
/// than two screens of rows, the first frame builds no more than that, and the whole scroll makes only a few screens of containers. The 10,000-row checks are wall clock: a median over several steps, with the
/// Loose factor for a loaded box.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowListBudgetTests
{
    internal const int Groups = 5;
    internal const int PerGroup = 1000;
    internal const double WindowHeight = 800;
    private const int Visible = 26;   // 800 px / 32 px rows, plus the partly shown one

    // The specs' wall-clock budgets (Home first paint 100 ms, Messages 16 ms a frame) times this, so a loaded box does not flake them (#2946). The counts above stay the tight guard.
    internal const double Loose = 5;
    private const int WarmUp = 3;
    private const int Timed = 31;

    internal static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowListBudgetTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    internal static void Settle(Window window)
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

    // A scroll step is ScrollTo plus Settle. The box runs several gates at once, so one step or the total flakes: a few warm-up steps (the pool fills and the code is jitted),
    // then Timed steps, and the assert is on the median. The failure message also names the slowest step.
    [Fact]
    public async Task A_full_window_scroll_step_of_10000_rows_has_a_median_inside_a_frame_budget_with_room_for_a_loaded_box() => await OnUi(() =>
    {
        var (window, list) = ShowTenThousand();
        var steps = ScrollSteps(window, list, stepPx: 3200);   // 100 rows: every visible row is new on every step
        var (median, slowest) = Summarize(steps);

        Assert.True(median <= 16 * Loose, string.Create(CultureInfo.InvariantCulture, $"median {median:0.0} ms, slowest {slowest:0.0} ms over {Timed} full-window scroll steps (after {WarmUp} warm-up), budget {16 * Loose:0} ms (16 ms x {Loose})"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_wheel_step_of_3_rows_in_10000_costs_a_frame_at_the_median() => await OnUi(() =>
    {
        var (window, list) = ShowTenThousand();
        var steps = ScrollSteps(window, list, stepPx: 96);   // 3 rows at 32 px: what one wheel notch moves
        var (median, slowest) = Summarize(steps);

        Assert.True(median <= 16 * Loose, string.Create(CultureInfo.InvariantCulture, $"median {median:0.0} ms, slowest {slowest:0.0} ms over {Timed} wheel steps of 3 rows (after {WarmUp} warm-up), budget {16 * Loose:0} ms (16 ms, one frame at 60 Hz, x {Loose})"));
        window.Close();
        return true;
    });

    private static (Window Window, RowList List) ShowTenThousand()
    {
        var list = new RowList { ItemsSource = RowGallery.Many(1, 10_000), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        return (window, list);
    }

    // WarmUp steps that are not timed, then Timed steps, each ScrollTo plus Settle, in ms.
    private static double[] ScrollSteps(Window window, RowList list, double stepPx)
    {
        var times = new double[Timed];
        var offset = 0d;
        for (var i = 0; i < WarmUp + Timed; i++)
        {
            offset += stepPx;
            var clock = Stopwatch.StartNew();
            list.ScrollTo(offset);
            Settle(window);
            clock.Stop();
            if (i >= WarmUp)
            {
                times[i - WarmUp] = clock.Elapsed.TotalMilliseconds;
            }
        }

        return times;
    }

    // Timed is odd, so the median is the middle step.
    private static (double Median, double Slowest) Summarize(double[] times)
    {
        var sorted = (double[])times.Clone();
        Array.Sort(sorted);
        return (sorted[sorted.Length / 2], sorted[^1]);
    }

    [Fact]
    public async Task A_recycle_reuses_the_rows_glyph_geometry_and_strips_and_builds_no_controls_per_row_scrolled_past() => await OnUi(() =>
    {
        var layoutsBefore = Row.TextLayoutsBuilt;
        var stripsBefore = RowActionsStrip.Made;
        var glyphBefore = RowGlyphPainter.Allocations;
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
        var layouts = Row.TextLayoutsBuilt - layoutsBefore;
        var strips = RowActionsStrip.Made - stripsBefore;
        var glyphs = RowGlyphPainter.Allocations - glyphBefore;
        Assert.True(list.GetVisualDescendants().OfType<Row>().All(r => !r.GetVisualChildren().Any()), "an idle row has no child control");
        Assert.True(strips <= 2, string.Create(CultureInfo.InvariantCulture, $"{strips} action strips made while no row was hot"));
        Assert.True(layouts <= (list.ContainersPrepared * 15L) + 15, string.Create(CultureInfo.InvariantCulture, $"{layouts} text layouts built for {list.ContainersPrepared} row fills: a row builds them once per item"));
        Assert.True(glyphs <= containers + 40, string.Create(CultureInfo.InvariantCulture, $"{glyphs} glyph geometries, pens and palettes built for {containers} containers"));
        window.Close();
        return true;
    });
}
