using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>
/// The grid is as tall as its cards (#3355). CardGrid measures its cards at the column width of its measure, then measures them again at the column width of its arrange. When the arrange is narrower
/// and a card wraps taller, the last card's bottom must not end below the height the grid reported.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CardGridArrangeHeightTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CardGridArrangeHeightTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_grid_is_as_tall_as_its_cards_after_an_arrange_at_a_narrower_width()
    {
        var (gridHeight, cardBottom, cardHeight) = await OnUi(() =>
        {
            // Measured at 900 px (two columns of 444, the card is 100 tall there), arranged at 430 px (one column of 430, the card is 200 tall there).
            var card = new WrappingCard();
            var grid = new CardGrid { MinColumnWidth = 420, Spacing = 12, Children = { card } };
            var window = new Window { Content = new NarrowerArrangeHost { Child = grid }, Width = 1000, Height = 600 };
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var result = (grid.DesiredSize.Height, card.Bounds.Bottom, card.Bounds.Height);
            window.Close();
            return result;
        });

        Assert.Equal(200, cardHeight);   // setup check: the card did take the taller height at the arrange width
        Assert.True(gridHeight >= cardBottom, $"the grid reported {gridHeight} but its card ends at {cardBottom}");
    }

    // A card whose height depends on its width the way wrapped text does: 100 tall at 440 px or wider, 200 tall below that.
    private sealed class WrappingCard : Control
    {
        protected override Size MeasureOverride(Size availableSize) => new(availableSize.Width, availableSize.Width < 440 ? 200 : 100);
    }

    // Measures its child at 900 px and arranges it at 430 px, as a parent does when the width it measured with is not the width it gets.
    private sealed class NarrowerArrangeHost : Decorator
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            Child?.Measure(new Size(900, double.PositiveInfinity));
            return Child?.DesiredSize ?? default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Child?.Arrange(new Rect(0, 0, 430, Child.DesiredSize.Height));
            return finalSize;
        }
    }
}
