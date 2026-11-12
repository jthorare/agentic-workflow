using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The card grid's columns (#2905): as many columns of at least the minimum width as fit with the gap, at least one.</summary>
[Lane(TestLane.Unit)]
public sealed class CardGridTests
{
    [Theory]
    [InlineData(1232, 2)]
    [InlineData(1392, 3)]
    [InlineData(952, 2)]
    [InlineData(1284, 3)]
    [InlineData(1283, 2)]
    [InlineData(851, 1)]
    [InlineData(300, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void Columns_are_as_many_420_px_columns_as_fit_with_a_12_px_gap(double width, int columns) =>
        Assert.Equal(columns, CardGrid.ColumnsFor(width, minColumn: 420, gap: 12));

    [Fact]
    public void A_full_row_of_columns_ends_on_the_right_edge()
    {
        var width = CardGrid.ColumnWidth(1232, columns: 2, gap: 12);

        Assert.Equal(1232, (2 * width) + 12, precision: 6);
    }
}
