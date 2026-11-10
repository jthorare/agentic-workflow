using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The shared status chip (#2545): one Border.status-chip per Sindri state, its fill, border and text resolved from the Brush.Status.* tokens, in the shape every page shares.</summary>
[Lane(TestLane.Integration)]
public sealed class StatusChipTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StatusChipTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    public static TheoryData<string, string> Variants() => new()
    {
        { "ok", "Ok" }, { "running", "Running" }, { "waiting", "Waiting" }, { "attention", "Attention" },
        { "error", "Error" }, { "neutral", "Neutral" }, { "draft", "Draft" },
    };

    // A brush's colour, looked up from the chip: the chip resolves its tokens through its window's theme, so the theme's brush is another
    // instance than the app's (Assert.Same failed on one colour), and the colour is what the reader sees.
    private static Color? ColourOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    private static Color? Token(StyledElement chip, string key) => ColourOf(chip.FindResource(key) as IBrush);

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task A_chip_resolves_its_fill_border_and_text_brushes_from_its_variant(string variant, string token)
    {
        var (drawn, expected, radius, height, thickness) = await OnUi(() =>
        {
            var text = new TextBlock { Text = variant };
            var chip = new Border { Child = text };
            chip.Classes.Add("status-chip");
            chip.Classes.Add(variant);
            var window = new Window { Content = chip };
            window.Show();
            window.UpdateLayout();
            var drawnColours = (ColourOf(chip.Background), ColourOf(chip.BorderBrush), ColourOf(text.Foreground));
            var tokens = (Token(chip, $"Brush.Status.{token}.Bg"), Token(chip, $"Brush.Status.{token}.Border"), Token(chip, $"Brush.Status.{token}.Fg"));
            return (drawnColours, tokens, chip.CornerRadius, chip.Bounds.Height, chip.BorderThickness);
        });

        Assert.NotNull(expected.Item1);
        Assert.NotNull(expected.Item2);
        Assert.NotNull(expected.Item3);
        Assert.Equal(expected, drawn);
        Assert.Equal(new CornerRadius(4), radius);
        Assert.Equal(20, height);
        Assert.Equal(new Thickness(1), thickness);
    }
}
