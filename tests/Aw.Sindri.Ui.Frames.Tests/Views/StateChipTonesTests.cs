using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// StateChipView draws the whole status-chip tone set (Theme/StatusChip.axaml, #3233): a chip a view names itself with Word and Tone, and an optional Glyph.
/// Each tone: the chip is at least 20 px tall, its word holds 4.5:1 on the chip's own fill over the panel, and the screen reader reads the word.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StateChipTonesTests
{
    public static TheoryData<string> Tones => ["ok", "running", "waiting", "attention", "error", "neutral", "draft"];

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StateChipTonesTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, StateChipView Chip) Show(string tone, string word, string? glyph = null)
    {
        var chip = new StateChipView { Word = word, Tone = tone, Glyph = glyph };
        var host = new Border { Background = Resource("Brush.Surface1"), Padding = new Thickness(20), Child = chip };
        var window = new Window { Content = host, Width = 400, Height = 120 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, chip);
    }

    private static IBrush Resource(string key) => Application.Current!.FindResource(key) as IBrush ?? Brushes.Magenta;

    private static string Cap(string tone) => string.Concat(char.ToUpperInvariant(tone[0]).ToString(), tone.AsSpan(1));

    [Theory]
    [MemberData(nameof(Tones))]
    public async Task A_tone_draws_its_word_at_least_20_px_tall_and_visible(string tone) => await OnUi(() =>
    {
        var (window, chip) = Show(tone, Cap(tone));

        Assert.True(chip.IsVisible);
        Assert.Equal(Cap(tone), chip.GetVisualDescendants().OfType<TextBlock>().First(t => !string.IsNullOrEmpty(t.Text)).Text);
        Assert.True(chip.Bounds.Height >= 20, string.Create(CultureInfo.InvariantCulture, $"{tone} chip is {chip.Bounds.Height} px tall"));
        Assert.Equal(1, chip.BorderThickness.Left);
        window.Close();
        return true;
    });

    [Theory]
    [MemberData(nameof(Tones))]
    public async Task A_tone_uses_its_status_brushes_and_holds_4_5_to_1(string tone) => await OnUi(() =>
    {
        var (window, chip) = Show(tone, Cap(tone));
        var text = chip.GetVisualDescendants().OfType<TextBlock>().First(t => !string.IsNullOrEmpty(t.Text));
        var key = "Brush.Status." + Cap(tone);

        Assert.Equal(Colour(Resource(key + ".Fg")), Colour(text.Foreground));
        Assert.Equal(Colour(Resource(key + ".Bg")), Colour(chip.Background));
        Assert.Equal(Colour(Resource(key + ".Border")), Colour(chip.BorderBrush));
        var panel = Colour(Resource("Brush.Surface1"));
        var fill = Over(Colour(chip.Background), panel);
        var ratio = Contrast(Colour(text.Foreground), fill);
        Assert.True(ratio >= 4.5, string.Create(CultureInfo.InvariantCulture, $"{tone}: {ratio:F2}:1"));
        window.Close();
        return true;
    });

    [Theory]
    [MemberData(nameof(Tones))]
    public async Task The_accessible_name_is_the_word_and_a_glyph_is_not_read(string tone) => await OnUi(() =>
    {
        var (window, chip) = Show(tone, Cap(tone), glyph: "●");

        Assert.Equal(Cap(tone), AutomationProperties.GetName(chip));
        var glyph = chip.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "●");
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(glyph));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_chip_without_a_glyph_draws_none_and_the_served_chip_still_works() => await OnUi(() =>
    {
        var (window, chip) = Show("ok", "Active");
        var served = new StateChipView { Chip = new StateChip("PARKED", ChipTone.Attn) };

        _ = Assert.Single(chip.GetVisualDescendants().OfType<TextBlock>());
        Assert.Contains(served.Classes, c => string.Equals(c, "attn", StringComparison.Ordinal));
        window.Close();
        return true;
    });

    private static Color Colour(IBrush? brush) => brush is ISolidColorBrush b ? Color.FromArgb((byte)(b.Color.A * b.Opacity), b.Color.R, b.Color.G, b.Color.B) : default;

    private static Color Over(Color top, Color under)
    {
        var a = top.A / 255d;
        byte Mix(byte t, byte u) => (byte)Math.Round((t * a) + (u * (1 - a)), MidpointRounding.AwayFromZero);
        return Color.FromRgb(Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B));
    }

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
}
