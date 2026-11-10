using System.Globalization;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Work table's columns (F3 spec work §3, #2551): a table too narrow for them keeps Title, Status and Age (and the row's action), and drops Repo and Stage. The grid tests run on the headless session's thread so they never take the UI thread from it (#1640).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class PageLayoutBandTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PageLayoutBandTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(759, "Tight")]
    [InlineData(760, "Narrow")]
    [InlineData(999, "Narrow")]
    [InlineData(1000, "Mid")]
    [InlineData(1120, "Wide")]
    public void The_compact_band_starts_under_760(double width, string expected) =>
        Assert.Equal(expected, PageLayout.For(width).ToString());

    [Fact]
    public async Task The_compact_band_keeps_title_status_and_age() => await OnUi(() =>
    {
        var grid = SixColumns();

        PageLayout.Apply(grid, new PageLayout.ColumnPlan(700, HasRepo: true, HasAction: true));   // 880 px of columns do not fit 700: Stage then Repo go

        Assert.Equal([true, false, true, false, true, true], grid.Children.Select(c => c.IsVisible));
        return true;
    });

    [Theory]
    [InlineData(1120)]
    [InlineData(1000)]
    [InlineData(760)]
    [InlineData(500)]
    public async Task The_columns_beside_the_title_leave_it_160_px_whenever_the_table_is_wide_enough_for_them(double width) => await OnUi(() =>
    {
        var grid = SixColumns();

        PageLayout.Apply(grid, new PageLayout.ColumnPlan(width, HasRepo: true, HasAction: true));

        var fixedWidth = grid.ColumnDefinitions.Skip(1).Sum(c => c.Width.Value);
        Assert.True(width - fixedWidth >= 160 || width < 620, string.Create(CultureInfo.InvariantCulture, $"{width}: the title column gets {width - fixedWidth} px"));
        Assert.Equal(Avalonia.Controls.GridUnitType.Star, grid.ColumnDefinitions[0].Width.GridUnitType);
        return true;
    });

    private static Avalonia.Controls.Grid SixColumns()
    {
        var grid = new Avalonia.Controls.Grid();
        for (var i = 0; i < 6; i++)
        {
            grid.ColumnDefinitions.Add(new Avalonia.Controls.ColumnDefinition());
            grid.Children.Add(new Avalonia.Controls.Border { [Avalonia.Controls.Grid.ColumnProperty] = i });
        }

        return grid;
    }
}
