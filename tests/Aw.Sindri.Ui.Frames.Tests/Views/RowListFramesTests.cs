using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The RowList's grammar in the real control (#2978): row pitch, aligned fact columns, the selected row, group headers, the sticky header, the peek's 3-column cut
/// and ellipsis. A row is flat (#3189): the tests read what it drew through Row.Layout (cells by column key, the age and title rectangles, the selection bar);
/// a group header has the class group-header.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowListFramesTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowListFramesTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, RowList List, Grid Page, PeekPane Peek) Show(List<GalleryGroup> groups, double width = 1440, double height = 600)
    {
        var list = new RowList { ItemsSource = groups, Columns = RowGallery.Columns };
        var peek = new PeekPane { IsVisible = false };
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(peek, 1);
        page.Children.Add(list);
        page.Children.Add(peek);
        var window = new Window { Content = page, Width = width, Height = height };
        window.Show();
        Settle(window);
        return (window, list, page, peek);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Row> Rows(Visual root) => [.. root.GetVisualDescendants().OfType<Row>().Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0).OrderBy(r => Top(r, root))];

    private static double Top(Visual v, Visual root) => v.TranslatePoint(default, root)?.Y ?? double.NaN;

    private static Color Brush(string key) => Application.Current!.FindResource(key) is ISolidColorBrush b ? b.Color : default;

    [Theory]
    [InlineData(false, 32)]
    [InlineData(true, 40)]
    public async Task Rows_are_32_px_apart_and_40_when_comfortable(bool comfortable, double pitch) => await OnUi(() =>
    {
        var (window, list, _, _) = Show(RowGallery.Three());
        window.Classes.Set("comfortable", comfortable);   // on the root, the way the grammar puts it on .app
        Settle(window);

        var tops = Rows(list).Select(r => Top(r, list)).ToList();

        Assert.True(tops.Count >= 8, $"{tops.Count} rows drawn");
        Assert.All(tops.Zip(tops.Skip(1), (a, b) => b - a).Where(d => d < 60), d => Assert.Equal(pitch, d, 0.01));   // a group header between two rows is longer than a row
        window.Close();
        return true;
    });

    [Fact]
    public async Task Every_fact_column_starts_at_the_same_x_on_every_row_and_the_age_ends_at_the_same_x() => await OnUi(() =>
    {
        var (window, list, _, _) = Show(RowGallery.Three());
        var rows = Rows(list);

        Assert.True(rows.Count >= 8, $"{rows.Count} rows drawn");
        foreach (var column in RowGallery.Columns)
        {
            var lefts = rows.Select(r => CellOf(r, column.Key).Bounds.X).Distinct().ToList();
            Assert.True(lefts.Count == 1, $"column {column.Key} starts at {string.Join(", ", lefts)}");
        }

        var rights = rows.Select(r => r.Layout.AgeRect.Right).Distinct().ToList();
        Assert.True(rights.Count == 1, $"age ends at {string.Join(", ", rights)}");
        var wide = CellOf(rows[0], "ref").Bounds.Width;
        var narrow = CellOf(rows[0], "status").Bounds.Width;
        Assert.Equal(136, wide, 0.01);
        Assert.Equal(104, narrow, 0.01);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_selected_row_has_a_2_px_primary_bar_inside_the_ring_and_a_primary_soft_fill() => await OnUi(() =>
    {
        var groups = RowGallery.Three();
        var (window, list, _, _) = Show(groups);
        list.SelectedItem = groups[0].Rows[3];
        Settle(window);

        var selected = Rows(list).Where(r => r.Classes.Contains("sel")).ToList();

        var row = Assert.Single(selected);
        Assert.Same(groups[0].Rows[3], row.DataContext);
        var layout = row.Layout;
        var bar = Assert.IsType<Rect>(layout.SelBar);
        Assert.Equal(Row.RingStroke, bar.X, 0.01);   // inside the ring's 2 px slot, so nothing moves when the cursor arrives
        Assert.Equal(Row.RingStroke, bar.Y, 0.01);
        Assert.Equal(2, bar.Width, 0.01);
        Assert.Equal(row.Bounds.Height - (2 * Row.RingStroke), bar.Height, 0.01);
        Assert.Equal(Brush("Brush.Primary"), Assert.IsType<ISolidColorBrush>(layout.SelBarInk, exactMatch: false).Color);
        Assert.Equal(Brush("Brush.PrimarySoft"), Assert.IsType<ISolidColorBrush>(row.Background, exactMatch: false).Color);
        Assert.All(Rows(list).Where(r => !ReferenceEquals(r, row)), r => Assert.Null(r.Layout.SelBar));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_group_header_is_28_px_tall() => await OnUi(() =>
    {
        var (window, list, _, _) = Show(RowGallery.Three());

        var headers = list.GetVisualDescendants().OfType<Control>().Where(c => c.Classes.Contains("group-header") && c.IsEffectivelyVisible).ToList();

        Assert.NotEmpty(headers);
        Assert.All(headers, h => Assert.Equal(28, h.Bounds.Height, 0.01));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_sticky_header_stays_while_its_group_scrolls_and_the_next_header_pushes_it_out() => await OnUi(() =>
    {
        var groups = RowGallery.Three();
        var (window, list, _, _) = Show(groups);
        const double SecondGroupTop = 8 + 28 + (14 * 32) + 8;   // list padding, a header, 14 rows, the 8 px group gap

        Assert.Null(list.StickyGroup);
        list.ScrollTo(100);
        Settle(window);
        Assert.Same(groups[0], list.StickyGroup);
        Assert.Equal(0, list.StickyPush, 0.01);

        list.ScrollTo(SecondGroupTop - 10);   // the second header is 10 px below the top: 18 px of the sticky one's 28 are pushed out
        Settle(window);
        Assert.Same(groups[0], list.StickyGroup);
        Assert.Equal(-18, list.StickyPush, 0.5);

        list.ScrollTo(SecondGroupTop + 1);
        Settle(window);
        Assert.Same(groups[1], list.StickyGroup);
        Assert.Equal(0, list.StickyPush, 0.01);
        window.Close();
        return true;
    });

    [Fact]
    public async Task With_the_peek_open_the_pane_is_440_wide_and_only_the_first_3_fact_columns_stay() => await OnUi(() =>
    {
        var groups = RowGallery.Three();
        var (window, list, _, peek) = Show(groups);
        Assert.Equal(RowGallery.Columns.Count, VisibleFacts(Rows(list)[0]).Count);

        list.SelectedItem = groups[0].Rows[0];
        list.IsPeekOpen = true;
        peek.IsVisible = true;
        Settle(window);

        Assert.Equal(440, peek.Bounds.Width, 0.01);
        Assert.Equal(3, list.PeekColumns);
        var shown = VisibleFacts(Rows(list)[0]);
        Assert.Equal([.. RowGallery.Columns.Take(3).Select(c => c.Key)], shown, StringComparer.Ordinal);
        window.Close();
        return true;
    });

    private static List<string> VisibleFacts(Row row) => [.. row.Layout.Cells.Select(c => c.Key)];

    private static RowCell CellOf(Row row, string key) => row.Layout.Cells.Single(c => string.Equals(c.Key, key, StringComparison.Ordinal));

    [Fact]
    public async Task A_long_title_and_a_long_ref_are_cut_with_an_ellipsis_and_do_not_wrap() => await OnUi(() =>
    {
        var groups = RowGallery.Three();
        var (window, list, _, _) = Show(groups);

        var rows = Rows(list);
        var longTitle = rows.First(r => ReferenceEquals(r.DataContext, groups[0].Rows[1]));
        var title = longTitle.Layout;
        Assert.True(title.TitleTrimmed);   // the title is cut with an ellipsis and does not wrap: it is one line, narrower than the whole title
        Assert.True(title.TitleDesiredWidth > title.TitleRect.Width, string.Create(CultureInfo.InvariantCulture, $"desired {title.TitleDesiredWidth}, drawn {title.TitleRect.Width}"));
        Assert.Equal(32, longTitle.Bounds.Height, 0.01);

        var longRef = rows.First(r => ReferenceEquals(r.DataContext, groups[0].Rows[2]));
        var text = CellOf(longRef, "ref").Text;
        Assert.Contains("…", text, StringComparison.Ordinal);
        Assert.EndsWith("/exports", text, StringComparison.Ordinal);   // the middle goes, the end that tells refs apart stays
        Assert.StartsWith("acme", text, StringComparison.Ordinal);
        window.Close();
        return true;
    });
}
