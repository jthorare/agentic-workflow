using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Theme;

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

    // A brush's colour and opacity, looked up from the chip: the chip resolves its tokens through its window's theme, so the theme's brush is
    // another instance than the app's (Assert.Same failed on one colour), and colour and opacity are what the reader sees. The opacity counts:
    // Ok's fill, border and text share Color.Success at 0.14, 0.3 and 1, so a chip with them swapped has the right colours.
    private static (Color Colour, double Opacity)? Paint(IBrush? brush) => brush is ISolidColorBrush solid ? (solid.Color, solid.Opacity) : null;

    private static (Color Colour, double Opacity)? Token(StyledElement chip, string key) => Paint(chip.FindResource(key) as IBrush);

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
            var drawnColours = (Paint(chip.Background), Paint(chip.BorderBrush), Paint(text.Foreground));
            var tokens = (Token(chip, $"Brush.Status.{token}.Bg"), Token(chip, $"Brush.Status.{token}.Border"), Token(chip, $"Brush.Status.{token}.Fg"));
            return (drawnColours, tokens, chip.CornerRadius, chip.Bounds.Height, chip.BorderThickness);
        });

        _ = Assert.NotNull(expected.Item1);
        _ = Assert.NotNull(expected.Item2);
        _ = Assert.NotNull(expected.Item3);
        Assert.Equal(expected, drawn);
        Assert.Equal(new CornerRadius(4), radius);
        Assert.Equal(20, height);
        Assert.Equal(new Thickness(1), thickness);
    }

    // ideas #2674: a fixed 20 px chip gave its word 18 px, and the caption line is 16 x 1.3 = 20.8 px at the largest text size, so the word
    // was cut. The scale is applied, checked and reset without yielding the UI thread, so no other test sees it.
    [Fact]
    public async Task A_chip_holds_its_whole_line_at_the_largest_text_size()
    {
        var (line, inside) = await OnUi(() =>
        {
            var resources = Application.Current!.Resources;
            TextScale.Apply(resources, TextScale.Max);
            try
            {
                var text = new TextBlock { Text = "Waiting" };
                var chip = new Border { Child = text };
                chip.Classes.Add("status-chip");
                chip.Classes.Add("waiting");
                var window = new Window { Content = chip };
                window.Show();
                window.UpdateLayout();
                text.Measure(Size.Infinity);
                var natural = text.DesiredSize.Height;
                var room = chip.Bounds.Height - chip.BorderThickness.Top - chip.BorderThickness.Bottom - chip.Padding.Top - chip.Padding.Bottom;
                window.Close();
                return (natural, room);
            }
            finally
            {
                TextScale.Apply(resources, 1.0);
            }
        });

        Assert.True(line > 20, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the 1.3 caption line is {line} px, not past 20: the scale did not apply"));
        Assert.True(inside >= line - 0.01, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the chip gives its word {inside} px of its {line} px line"));
    }
}
