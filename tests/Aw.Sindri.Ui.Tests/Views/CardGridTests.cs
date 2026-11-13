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

    private static string Shape(List<int[]> rows) => string.Join(" | ", rows.Select(r => string.Join(',', r)));

    private static bool[] Cards(int count, params int[] fullAt) => [.. Enumerable.Range(0, count).Select(i => fullAt.Contains(i))];

    [Fact]
    public void By_default_a_run_fills_row_by_row() =>
        Assert.Equal("0,1 | 2,3 | 4", Shape(CardGrid.Place(Cards(5), columns: 2, columnMajor: false)));

    [Fact]
    public void Column_major_puts_the_first_ceil_n_over_columns_down_the_left_column()
    {
        // n = 5 in 2 columns: the left column holds 0, 1, 2 and the right 3, 4, so the last row has one card.
        Assert.Equal("0,3 | 1,4 | 2", Shape(CardGrid.Place(Cards(5), columns: 2, columnMajor: true)));
        Assert.Equal("0,2 | 1,3", Shape(CardGrid.Place(Cards(4), columns: 2, columnMajor: true)));
        Assert.Equal("0", Shape(CardGrid.Place(Cards(1), columns: 2, columnMajor: true)));
    }

    [Fact]
    public void Column_major_with_one_column_is_the_plain_order() =>
        Assert.Equal("0 | 1 | 2", Shape(CardGrid.Place(Cards(3), columns: 1, columnMajor: true)));

    [Fact]
    public void A_full_row_child_splits_the_runs_and_each_run_is_column_major_on_its_own() =>
        // cards 0-2, a full row at 3, then cards 4-6.
        Assert.Equal("0,2 | 1 | 3 | 4,6 | 5", Shape(CardGrid.Place(Cards(7, fullAt: 3), columns: 2, columnMajor: true)));

    [Fact]
    public void Column_major_across_three_columns_leaves_only_trailing_cells_empty() =>
        Assert.Equal("0,2,4 | 1,3", Shape(CardGrid.Place(Cards(5), columns: 3, columnMajor: true)));
}
