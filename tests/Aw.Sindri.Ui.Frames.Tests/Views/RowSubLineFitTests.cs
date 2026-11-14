using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A long sub-line must not run into the fact cells (#3189, review of #3499): the id and the sub-line used to keep their full width and only the title got what was left.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowSubLineFitTests
{
    private const string LongSub = "a sub-line that is far wider than the room a title column of 200 px leaves for it, so it has to end with an ellipsis";

    [Fact]
    public async Task A_long_sub_line_ends_before_the_fact_cells() => await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowSubLineFitTests).Assembly).Dispatch(() =>
    {
        var list = new RowList { ItemsSource = new[] { new SingleGroup(new LongSubRow()) }, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1100, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var row = list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);

        var layout = row.Layout;
        var sub = Assert.IsType<Rect>(layout.SubRect);
        var factsStart = layout.Cells.Min(c => c.Bounds.X) - 12;   // a cell starts one 12 px gap after the facts column

        Assert.True(sub.Right <= factsStart + 0.5, string.Create(CultureInfo.InvariantCulture, $"the sub-line ends at {sub.Right:0.0}, the facts start at {factsStart:0.0}"));
        window.Close();
        return true;
    }, TestContext.Current.CancellationToken);

    private sealed class SingleGroup(IRowItem row) : IRowGroupHeading
    {
        public string Heading => "Needs you";

        public string? Summary => null;

        public IEnumerable Items { get; } = new[] { row };

        public bool IsCollapsed { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }

    private sealed record LongSubRow : IRowItem
    {
        public string Id => "r-0001";

        public string Key => "test:pr:long-sub";

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string Title => "A title that wants room too";

        public string? SubLine => LongSub;

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
