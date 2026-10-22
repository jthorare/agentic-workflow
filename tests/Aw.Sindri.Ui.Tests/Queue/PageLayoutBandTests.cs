using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Work table's width bands (F3 spec work §3): under 760 px the compact band keeps Title, Status and Age (and the row's action), and drops Repo and Stage.</summary>
[Lane(TestLane.Unit)]
public sealed class PageLayoutBandTests
{
    [Theory]
    [InlineData(759, "Tight")]
    [InlineData(760, "Narrow")]
    [InlineData(999, "Narrow")]
    [InlineData(1000, "Mid")]
    [InlineData(1120, "Wide")]
    public void The_compact_band_starts_under_760(double width, string expected) =>
        Assert.Equal(expected, PageLayout.For(width).ToString());

    [Fact]
    public void The_compact_band_keeps_title_status_and_age()
    {
        var grid = new Avalonia.Controls.Grid();
        for (var i = 0; i < 6; i++)
        {
            grid.ColumnDefinitions.Add(new Avalonia.Controls.ColumnDefinition());
            grid.Children.Add(new Avalonia.Controls.Border { [Avalonia.Controls.Grid.ColumnProperty] = i });
        }

        PageLayout.Apply(grid, PageTier.Tight);

        Assert.Equal([true, false, true, false, true, true], grid.Children.Select(c => c.IsVisible));
    }
}
