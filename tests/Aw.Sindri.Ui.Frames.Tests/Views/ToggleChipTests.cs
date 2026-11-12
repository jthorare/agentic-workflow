using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The toggle chip (#3234): the Advanced mode toggle with hover, pressed, focus and checked looks, and the same part not clickable as the
/// mode chip, the findings chip and the run row's Test chip. Size, focus ring, checked look, the word's contrast on every fill it can
/// sit on, and the accessible name with the toggle state.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ToggleChipTests
{
    public static TheoryData<string> Tones => ["neutral", "ok", "attention", "error"];

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ToggleChipTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, ToggleChip Chip) Show(string word, string tone = "neutral", bool interactive = true)
    {
        var chip = new ToggleChip { Word = word, Tone = tone, Interactive = interactive };
        var host = new Border { Background = Resource("Brush.Surface1"), Padding = new Thickness(20), Child = chip };
        var window = new Window { Content = host, Width = 400, Height = 120 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, chip);
    }

    private static ContentPresenter Face(ToggleChip chip) =>
        chip.GetVisualDescendants().OfType<ContentPresenter>().First(p => string.Equals(p.Name, "PART_ContentPresenter", StringComparison.Ordinal));

    private static TextBlock Text(ToggleChip chip) =>
        chip.GetVisualDescendants().OfType<TextBlock>().First(t => !string.IsNullOrEmpty(t.Text));

    private static IBrush Resource(string key) => Application.Current!.FindResource(key) as IBrush ?? Brushes.Magenta;

    private static void Pseudo(ToggleChip chip, string name, bool on)
    {
        ((IPseudoClasses)chip.Classes).Set(name, on);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task A_clickable_chip_is_at_least_24_px_tall_and_a_status_chip_20_to_24() => await OnUi(() =>
    {
        var (window, toggle) = Show("Advanced");
        var (window2, status) = Show("Active", "ok", interactive: false);

        Assert.True(toggle.Bounds.Height >= 24, string.Create(CultureInfo.InvariantCulture, $"toggle is {toggle.Bounds.Height} px tall"));
        Assert.InRange(status.Bounds.Height, 20, 24);
        Assert.True(toggle.Bounds.Width >= 24, "a pointer target is at least 24 px wide");
        window.Close();
        window2.Close();
        return true;
    });

    [Fact]
    public async Task The_focus_ring_is_2_px_PrimaryHover_and_holds_3_to_1_on_the_panel() => await OnUi(() =>
    {
        var (window, chip) = Show("Advanced");
        var rest = Face(chip).Bounds.Size;
        Assert.True(chip.Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var face = Face(chip);
        Assert.Contains(":focus-visible", chip.Classes, StringComparer.Ordinal);
        Assert.Equal(2, face.BorderThickness.Left);
        Assert.Equal(Colour(Resource("Brush.PrimaryHover")), Colour(face.BorderBrush));
        Assert.Equal(rest, face.Bounds.Size);   // the ring replaces the 1 px border and 1 px of padding: the chip does not grow
        Assert.True(Contrast(Colour(face.BorderBrush), Colour(Resource("Brush.Surface1"))) >= 3, "focus ring against the panel");
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_checked_chip_takes_PrimarySoft_and_a_Primary_border_with_Ink_on_it() => await OnUi(() =>
    {
        var (window, chip) = Show("Advanced");
        var before = Colour(Face(chip).Background);
        chip.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var face = Face(chip);
        Assert.NotEqual(before, Colour(face.Background));
        Assert.Equal(Colour(Resource("Brush.PrimarySoft")), Colour(face.Background));
        Assert.Equal(Colour(Resource("Brush.Primary")), Colour(face.BorderBrush));
        Assert.Equal(Colour(Resource("Brush.Ink")), Colour(Text(chip).Foreground));
        Assert.True(Contrast(Colour(Text(chip).Foreground), Over(Colour(face.Background), Colour(Resource("Brush.Surface1")))) >= 4.5, "checked word");
        window.Close();
        return true;
    });

    [Theory]
    [MemberData(nameof(Tones))]
    public async Task The_word_holds_4_5_to_1_at_rest_under_the_pointer_and_pressed(string tone) => await OnUi(() =>
    {
        var (window, chip) = Show("Word", tone);
        var panel = Colour(Resource("Brush.Surface1"));

        double Ratio() => Contrast(Colour(Text(chip).Foreground), Over(Colour(Face(chip).Background), panel));

        var rest = Ratio();
        Pseudo(chip, ":pointerover", on: true);
        Assert.Equal(Colour(Resource("Brush.Clickable.Hover")), Colour(Face(chip).Background));
        var hover = Ratio();
        Pseudo(chip, ":pressed", on: true);
        Assert.Equal(Colour(Resource("Brush.Clickable.Pressed")), Colour(Face(chip).Background));
        var pressed = Ratio();

        Assert.True(rest >= 4.5 && hover >= 4.5 && pressed >= 4.5, string.Create(CultureInfo.InvariantCulture, $"{tone}: rest {rest:F2}, hover {hover:F2}, pressed {pressed:F2}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_chip_uses_the_status_brushes_of_its_tone_at_rest() => await OnUi(() =>
    {
        var (window, chip) = Show("Fix 2 things", "error");

        Assert.Equal(Colour(Resource("Brush.Status.Error.Fg")), Colour(Text(chip).Foreground));
        Assert.Equal(Colour(Resource("Brush.Status.Error.Bg")), Colour(Face(chip).Background));
        Assert.Equal(Colour(Resource("Brush.Status.Error.Border")), Colour(Face(chip).BorderBrush));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_accessible_name_is_the_word_and_the_toggle_state_follows_IsChecked() => await OnUi(() =>
    {
        var (window, chip) = Show("Advanced");
        var peer = ControlAutomationPeer.CreatePeerForElement(chip);
        var toggle = peer.GetProvider<IToggleProvider>() ?? throw new InvalidOperationException("no toggle provider");

        Assert.Equal("Advanced", peer.GetName());
        Assert.Equal("Off", toggle.ToggleState.ToString());
        chip.IsChecked = true;
        Assert.Equal("On", toggle.ToggleState.ToString());
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_status_chip_cannot_be_clicked_or_focused_and_reads_as_text() => await OnUi(() =>
    {
        var (window, chip) = Show("Not tested", "attention", interactive: false);

        Assert.False(chip.Focusable);
        Assert.False(chip.IsHitTestVisible);
        Assert.Equal(AutomationControlType.Text, ControlAutomationPeer.CreatePeerForElement(chip).GetAutomationControlType());
        Assert.Equal("Not tested", ControlAutomationPeer.CreatePeerForElement(chip).GetName());
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
