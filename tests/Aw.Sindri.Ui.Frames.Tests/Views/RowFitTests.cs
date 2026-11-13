using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// A row fits its window (#3189, review of #3499; grammar.md: "Padding, gaps and the title minimum never shrink"). Age and waits-on sit at the right edge, the title column keeps its 200 px, and
/// when the facts don't fit next to it the row drops them from the end, as the peek does. The drop depends on the width alone, so every row of a list shows the same columns.
/// With the gallery's five columns (four of 104 px and one of 136 px, each after a 12 px gap) a row needs about 1056 px to show them all.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowFitTests
{
    private const double TitleMin = 200;
    private const double CellGap = 12;   // a cell starts one gap after the facts column
    private const string LongTitle = "A title long enough to run well past the two hundred pixels of its column at body size";
    private const string LongSub = "a sub-line that is far wider than the room the title column leaves for it";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowFitTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, RowList List) Show(double width, params IRowItem[] rows)
    {
        var list = new RowList { ItemsSource = new[] { new Group(rows) }, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = width, Height = 400 };
        window.Show();
        Settle(window);
        return (window, list);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Row> Rows(RowList list) => [.. list.GetVisualDescendants().OfType<Row>().Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0)];

    private static string Say(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public async Task At_1000_px_age_and_waits_on_end_inside_the_row() => await OnUi(() =>
    {
        var (window, list) = Show(1000, new Item("r-0001", "Row 0", null));
        var row = Rows(list)[0];
        var layout = row.Layout;

        Assert.True(layout.WaitRect.Right <= row.Bounds.Width - 24 + 0.5, Say($"waits-on ends at {layout.WaitRect.Right:0.0}, the row's content ends at {row.Bounds.Width - 24:0.0}"));
        Assert.True(layout.AgeRect.Right <= layout.WaitRect.X + 0.5, Say($"age ends at {layout.AgeRect.Right:0.0}, waits-on starts at {layout.WaitRect.X:0.0}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task At_1000_px_the_row_drops_the_owner_column_so_the_title_keeps_its_200_px() => await OnUi(() =>
    {
        var (window, list) = Show(1000, new Item("r-0001", "Row 0", null));
        var layout = Rows(list)[0].Layout;

        Assert.Equal(["status", "progress", "diff", "ref"], layout.Cells.Select(c => c.Key));
        Assert.True(layout.TitleSlot.Width >= TitleMin - 0.5, Say($"the title column is {layout.TitleSlot.Width:0.0} px, its minimum is {TitleMin}"));
        Assert.True(layout.TitleSlot.Right <= layout.Cells.Min(c => c.Bounds.X) - CellGap + 0.5, Say($"the title column ends at {layout.TitleSlot.Right:0.0}, the facts start at {layout.Cells.Min(c => c.Bounds.X) - CellGap:0.0}"));
        window.Close();
        return true;
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task At_1000_px_a_long_title_and_its_sub_line_end_before_the_first_fact(bool withSub) => await OnUi(() =>
    {
        var (window, list) = Show(1000, new Item("r-0001", LongTitle, withSub ? LongSub : null));
        var layout = Rows(list)[0].Layout;
        var firstFact = layout.Cells.Min(c => c.Bounds.X);

        Assert.True(layout.TitleTrimmed, "the long title is cut with an ellipsis");
        Assert.True(layout.TitleRect.Right <= firstFact - CellGap + 0.5, Say($"the title ends at {layout.TitleRect.Right:0.0}, the first fact starts at {firstFact:0.0}"));
        if (layout.SubRect is { } sub)
        {
            Assert.True(sub.Right <= firstFact - CellGap + 0.5, Say($"the sub-line ends at {sub.Right:0.0}, the first fact starts at {firstFact:0.0}"));
        }

        window.Close();
        return true;
    });

    [Fact]
    public async Task At_400_px_no_fact_fits_and_the_title_takes_what_is_left_before_age() => await OnUi(() =>
    {
        var (window, list) = Show(400, new Item("r-0001", LongTitle, LongSub));
        var layout = Rows(list)[0].Layout;

        Assert.Empty(layout.Cells);
        Assert.True(layout.TitleSlot.Width > 0, Say($"the title column is {layout.TitleSlot.Width:0.0} px"));
        Assert.True(layout.TitleSlot.Right <= layout.AgeRect.X - CellGap + 0.5, Say($"the title column ends at {layout.TitleSlot.Right:0.0}, age starts at {layout.AgeRect.X:0.0}"));
        Assert.True(layout.TitleRect.Right <= layout.AgeRect.X - CellGap + 0.5, Say($"the title ends at {layout.TitleRect.Right:0.0}, age starts at {layout.AgeRect.X:0.0}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Every_row_of_a_list_shows_the_same_columns_whatever_its_text() => await OnUi(() =>
    {
        var (window, list) = Show(1000, new Item("r-0001", "Row 0", null), new Item("r-0002", LongTitle, LongSub), new Item("r-0003", "Row 2", LongSub));
        var shown = Rows(list).Select(r => string.Join(",", r.Layout.Cells.Select(c => c.Key))).Distinct().ToList();

        _ = Assert.Single(shown);
        window.Close();
        return true;
    });

    [Theory]
    [InlineData(1280, 3)]
    [InlineData(700, 2)]
    public async Task With_the_peek_open_a_row_shows_the_smaller_of_the_peek_count_and_what_fits(double width, int expected) => await OnUi(() =>
    {
        var (window, list) = Show(width, new Item("r-0001", "Row 0", null));
        list.PeekColumns = 3;
        list.IsPeekOpen = true;
        Settle(window);
        var layout = Rows(list)[0].Layout;

        Assert.Equal(expected, layout.Cells.Count);
        Assert.True(layout.TitleSlot.Width >= TitleMin - 0.5, Say($"the title column is {layout.TitleSlot.Width:0.0} px at {width} px"));
        window.Close();
        return true;
    });

    private sealed class Group(IReadOnlyList<IRowItem> rows) : IRowGroupHeading
    {
        public string Heading => "Needs you";

        public string? Summary => null;

        public IEnumerable Items { get; } = rows;

        public bool IsCollapsed { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }

    private sealed record Item(string Id, string Title, string? SubLine) : IRowItem
    {
        public string Key => "test:pr:" + Id;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string Kind => "pr";

        public RowStateClass StateClass => RowStateClass.Waiting;

        public string? StateKey => "in-review";

        public bool Unread => false;

        public IReadOnlyDictionary<string, RowFact> Facts { get; } = RowGallery.Row(0).Facts;

        public string Age => "1m";

        public RowWaitsOn? WaitsOn => null;

        public IReadOnlyList<RowAction> Actions { get; } = [];
    }
}
