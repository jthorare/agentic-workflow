using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The card grid's column-major fill (#3478) as laid out: a run of cards goes down the left column first, a row is as tall as its tallest card (the row-major rule), a hidden card takes no cell,
/// and a grid without ColumnMajor still fills row by row. Width 1000 with 420 px columns and a 12 px gap is two columns of 494.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CardGridLayoutFramesTests
{
    private const string Right = "506";   // a 494 px column and the 12 px gap

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CardGridLayoutFramesTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (CardGrid Grid, List<Border> Cards) Lay(bool columnMajor, double[] heights, int hidden = -1)
    {
        var grid = new CardGrid { ColumnMajor = columnMajor };
        var cards = heights.Select((h, i) => new Border { Height = h, IsVisible = i != hidden }).ToList();
        foreach (var card in cards)
        {
            grid.Children.Add(card);
        }

        grid.Measure(new Size(1000, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, 1000, grid.DesiredSize.Height));
        return (grid, cards);
    }

    private static string At(Border card) => string.Create(CultureInfo.InvariantCulture, $"{card.Bounds.X:0},{card.Bounds.Y:0}");

    [Fact]
    public Task Column_major_stacks_each_column_on_its_own_so_a_tall_card_leaves_no_gap_beside_it() => OnUi(() =>
    {
        // Left column: 0 (20), 1 (60), 2 (20) at y 0, 32, 104. Right column: 3 (40), 4 (20) at y 0, 52. Card 4 sits at card 3's bottom plus the gap, not under the tall card 1 (#3493).
        var (_, cards) = Lay(columnMajor: true, [20, 60, 20, 40, 20]);

        string[] expected = ["0,0", "0,32", "0,104", Right + ",0", Right + ",52"];
        Assert.Equal(expected, cards.Select(At), StringComparer.Ordinal);
        return true;
    });

    [Fact]
    public Task Without_ColumnMajor_a_row_is_as_tall_as_its_tallest_card() => OnUi(() =>
    {
        // Rows (0, 1) are 60 tall, (2, 3) 40 tall, (4) alone: the paired rows stay (the Projects page and #3421 rely on them).
        var (_, cards) = Lay(columnMajor: false, [20, 60, 20, 40, 20]);

        string[] expected = ["0,0", Right + ",0", "0,72", Right + ",72", "0,124"];
        Assert.Equal(expected, cards.Select(At), StringComparer.Ordinal);
        return true;
    });

    [Fact]
    public Task A_hidden_card_leaves_no_gap_and_the_count_splits_on_the_visible_ones() => OnUi(() =>
    {
        // Card 1 is hidden: the four visible cards split 2 and 2, (0, 3) and (2, 4).
        var (_, cards) = Lay(columnMajor: true, [30, 30, 30, 30, 30], hidden: 1);

        string[] expected = ["0,0", "0,0", "0,42", Right + ",0", Right + ",42"];
        Assert.Equal(expected, cards.Select(c => c.IsVisible ? At(c) : "0,0"), StringComparer.Ordinal);
        return true;
    });

    [Fact]
    public Task Without_ColumnMajor_the_cards_still_fill_row_by_row() => OnUi(() =>
    {
        var (_, cards) = Lay(columnMajor: false, [30, 30, 30, 30, 30]);

        string[] expected = ["0,0", Right + ",0", "0,42", Right + ",42", "0,84"];
        Assert.Equal(expected, cards.Select(At), StringComparer.Ordinal);
        return true;
    });
}
