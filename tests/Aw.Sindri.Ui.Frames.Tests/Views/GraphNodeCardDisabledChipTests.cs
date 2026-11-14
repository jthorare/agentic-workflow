using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3648: on a disabled graph card the status chip dims with the card (the card's own fill and border) while its word still reads at 3:1 on the card, and it comes back in full colour when the card is enabled again. The app has the dark theme only, so that is the one checked.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class GraphNodeCardDisabledChipTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphNodeCardDisabledChipTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, GraphNodeCard Card) Show(bool enabled)
    {
        var card = new GraphNodeCard { Title = "Ask the agent", Kind = "run_agent", StatusWord = "Failed", StatusTone = "error", StatusGlyph = "✕", IsEnabled = enabled, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = new Window { Width = 400, Height = 300, Content = card };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, card);
    }

    private static Color Colour(IBrush? brush) => brush is ISolidColorBrush b ? b.Color : default;

    private static double Contrast(Color a, Color b)
    {
        static double Lin(byte c)
        {
            var s = c / 255d;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        static double L(Color c) => (0.2126 * Lin(c.R)) + (0.7152 * Lin(c.G)) + (0.0722 * Lin(c.B));
        var (hi, lo) = (Math.Max(L(a), L(b)), Math.Min(L(a), L(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    [Fact]
    public async Task A_disabled_cards_chip_takes_the_cards_fill_and_border_and_its_word_holds_3_to_1() => await OnUi(() =>
    {
        var (window, card) = Show(enabled: false);

        var face = card.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Card");
        var chip = card.GetVisualDescendants().OfType<StateChipView>().Single();
        var word = chip.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Failed");
        Assert.Equal(Colour(face.Background), Colour(chip.Background));
        Assert.Equal(Colour(Application.Current!.FindResource("Brush.Wire") as IBrush), Colour(chip.BorderBrush));
        var ratio = Contrast(Colour(word.Foreground), Colour(face.Background));
        Assert.True(ratio >= 3.0, $"the dimmed chip's word is {ratio:F2}:1 on the card");
        Assert.Equal("Failed", chip.Word);   // the word and its tone are still served: only the paint dims
        window.Close();
        return true;
    });

    [Fact]
    public async Task An_enabled_card_keeps_the_chips_tone_and_enabling_again_restores_it() => await OnUi(() =>
    {
        var (window, card) = Show(enabled: true);
        var chip = card.GetVisualDescendants().OfType<StateChipView>().Single();
        var toned = Colour(chip.Background);
        var face = card.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Card");
        Assert.NotEqual(Colour(face.Background), toned);

        card.IsEnabled = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Colour(face.Background), Colour(chip.Background));

        card.IsEnabled = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(toned, Colour(chip.Background));
        window.Close();
        return true;
    });
}
