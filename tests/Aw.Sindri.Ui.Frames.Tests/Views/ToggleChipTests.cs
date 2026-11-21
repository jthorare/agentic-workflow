using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
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
    public static TheoryData<string> Tones => ["ok", "running", "waiting", "attention", "error", "neutral", "draft"];

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

    private static string Key(string tone, string part) => "Brush.Status." + char.ToUpperInvariant(tone[0]) + tone[1..] + "." + part;

    private static Ellipse Dot(ToggleChip chip) => chip.GetVisualDescendants().OfType<Ellipse>().First();

    private static Color Panel => Colour(Resource("Brush.Surface1"));

    private static Color FaceFill(ToggleChip chip) => Over(Colour(Face(chip).Background), Panel);

    [Theory]
    [MemberData(nameof(Tones))]
    public async Task Off_is_the_grey_pill_with_Ink_and_a_hollow_dot_in_the_state_colour(string tone) => await OnUi(() =>
    {
        var (window, chip) = Show("Word", tone);
        var dot = Dot(chip);

        Assert.Equal(Colour(Resource("Brush.Ink")), Colour(Text(chip).Foreground));
        Assert.Equal(Colour(Resource("Brush.Status.Neutral.Bg")), Colour(Face(chip).Background));
        Assert.Equal(Colour(Resource("Brush.Status.Neutral.Border")), Colour(Face(chip).BorderBrush));
        Assert.Equal(Colour(Resource(Key(tone, "Fg"))), Colour(dot.Stroke));
        Assert.Equal(0, Colour(dot.Fill).A);   // hollow
        window.Close();
        return true;
    });

    [Theory]
    [MemberData(nameof(Tones))]
    public async Task On_is_the_state_tint_with_a_state_border_Ink_and_a_filled_dot(string tone) => await OnUi(() =>
    {
        var (window, chip) = Show("Word", tone);
        chip.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        var dot = Dot(chip);

        Assert.Equal(Colour(Resource("Brush.Ink")), Colour(Text(chip).Foreground));
        Assert.Equal(Colour(Resource(Key(tone, "On"))), Colour(Face(chip).Background));
        Assert.Equal(Colour(Resource(Key(tone, "Fg"))), Colour(Face(chip).BorderBrush));
        Assert.Equal(Colour(Resource(Key(tone, "Fg"))), Colour(dot.Fill));   // filled: the cue that is not colour alone
        window.Close();
        return true;
    });

    [Theory]
    [MemberData(nameof(Tones))]
    public async Task The_chip_keeps_its_width_when_it_turns_on(string tone) => await OnUi(() =>
    {
        var (window, chip) = Show("Advanced", tone);
        var off = chip.Bounds.Width;
        chip.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Equal(off, chip.Bounds.Width);
        window.Close();
        return true;
    });

    [Theory]
    [MemberData(nameof(Tones))]
    public async Task Ink_holds_4_5_to_1_and_the_dot_and_an_on_border_3_to_1_off_and_on_at_rest_under_the_pointer_and_pressed(string tone) => await OnUi(() =>
    {
        var (window, chip) = Show("Word", tone);
        var low = new List<string>();

        void Check(string at)
        {
            var fill = FaceFill(chip);
            var ink = Contrast(Colour(Text(chip).Foreground), fill);
            var dot = Contrast(Over(Colour(Dot(chip).Stroke), fill), fill);
            var border = chip.IsChecked == true ? Contrast(Colour(Face(chip).BorderBrush), Panel) : 3;
            if (ink < 4.5 || dot < 3 || border < 3)
            {
                low.Add(string.Create(CultureInfo.InvariantCulture, $"{tone} {at}: ink {ink:F2}, dot {dot:F2}, border {border:F2}"));
            }
        }

        bool[] states = [false, true];
        foreach (var on in states)
        {
            chip.IsChecked = on;
            Pseudo(chip, ":pointerover", on: false);
            Pseudo(chip, ":pressed", on: false);
            Check(on ? "on rest" : "off rest");
            Pseudo(chip, ":pointerover", on: true);
            Check(on ? "on hover" : "off hover");
            Pseudo(chip, ":pressed", on: true);
            Check(on ? "on pressed" : "off pressed");
        }

        Assert.True(low.Count == 0, string.Join("; ", low));
        window.Close();
        return true;
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hover_and_pressed_lift_the_fill_and_the_focus_ring_replaces_the_border_without_growing_the_chip(bool on) => await OnUi(() =>
    {
        var (window, chip) = Show("Word", "ok");
        chip.IsChecked = on;
        Dispatcher.UIThread.RunJobs();
        var rest = Face(chip).Bounds.Size;
        Pseudo(chip, ":pointerover", on: true);
        Assert.Equal(Colour(Resource(on ? "Brush.Status.Ok.OnHover" : "Brush.Clickable.Hover")), Colour(Face(chip).Background));
        Pseudo(chip, ":pressed", on: true);
        Assert.Equal(Colour(Resource(on ? "Brush.Status.Ok.OnPressed" : "Brush.Clickable.Pressed")), Colour(Face(chip).Background));
        Pseudo(chip, ":pointerover", on: false);
        Pseudo(chip, ":pressed", on: false);

        Assert.True(chip.Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.Equal(2, Face(chip).BorderThickness.Left);
        Assert.Equal(Colour(Resource("Brush.PrimaryHover")), Colour(Face(chip).BorderBrush));
        Assert.Equal(rest, Face(chip).Bounds.Size);
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
