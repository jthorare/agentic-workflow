using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Workflows start card (#3234): rest, hover, pressed, focus, selected and disabled, with the tile's tokens; a card is roomier than a tile and always fills its column.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StartCardTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StartCardTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, StartCard Card, Border Chrome) Show()
    {
        var card = new StartCard
        {
            Content = new StackPanel { Children = { new TextBlock { Text = "From a template" }, new TextBlock { Text = "Pick a ready-made graph and change it." } } },
        };
        var window = new Window { Content = new StackPanel { Margin = new Thickness(20), Children = { card } }, Width = 400, Height = 240 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, card, card.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Chrome"));
    }

    private static Color Brush(string key) => Application.Current!.FindResource(key) is ISolidColorBrush b ? b.Color : default;

    private static Color Fill(Border chrome) => chrome.Background is ISolidColorBrush b ? b.Color : default;

    private static Color Edge(Border chrome) => chrome.BorderBrush is ISolidColorBrush b ? b.Color : default;

    private static Point Centre(Visual v, Visual root) => v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), root) ?? default;

    private static double Luminance(Color c)
    {
        static double Lin(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Lin(c.R)) + (0.7152 * Lin(c.G)) + (0.0722 * Lin(c.B));
    }

    private static double Contrast(Color a, Color b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    [Fact]
    public async Task At_rest_the_card_is_transparent_with_a_hairline_a_card_padding_and_fills_its_column() => await OnUi(() =>
    {
        var (window, card, chrome) = Show();

        Assert.Equal(Colors.Transparent, Fill(chrome));
        Assert.Equal(Brush("Brush.Hairline"), Edge(chrome));
        Assert.Equal(16, chrome.Padding.Left);
        Assert.Equal(360, card.Bounds.Width, 1);
        Assert.True(card.Bounds.Height >= 56, card.Bounds.Height.ToString(CultureInfo.InvariantCulture));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Hover_fills_Surface2_and_pressed_fills_Surface3() => await OnUi(() =>
    {
        var (window, card, chrome) = Show();
        var point = Centre(card, window);

        window.MouseMove(point, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Brush("Brush.Surface2"), Fill(chrome));

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Brush("Brush.Surface3"), Fill(chrome));
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        window.Close();
        return true;
    });

    // The one focus ring is the app-wide adorner (FluentOverrides.axaml, 2 to 4 px outside); the chrome keeps its own border, so a focused selected one still shows its Primary border inside the ring.
    [Fact]
    public async Task Keyboard_focus_keeps_the_chrome_border_and_leaves_the_ring_to_the_app_adorner() => await OnUi(() =>
    {
        var (window, card, chrome) = Show();
        var rest = Edge(chrome);

        Assert.True(card.Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(rest, Edge(chrome));
        Assert.Equal(Brush("Brush.Hairline"), Edge(chrome));
        Assert.Equal(1, chrome.BorderThickness.Left);

        card.IsSelected = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Brush("Brush.Primary"), Edge(chrome));
        Assert.Equal(1, chrome.BorderThickness.Left);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Selected_is_a_PrimarySoft_fill_with_a_Primary_border_and_survives_hover() => await OnUi(() =>
    {
        var (window, card, chrome) = Show();
        card.IsSelected = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Brush("Brush.PrimarySoft"), Fill(chrome));
        Assert.Equal(Brush("Brush.Primary"), Edge(chrome));
        window.MouseMove(Centre(card, window), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Brush("Brush.PrimarySoft"), Fill(chrome));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Disabled_dims_every_word_and_ignores_hover_and_clicks() => await OnUi(() =>
    {
        var (window, card, chrome) = Show();
        var clicks = 0;
        card.Click += (_, _) => clicks++;
        card.IsEnabled = false;
        Dispatcher.UIThread.RunJobs();
        var point = Centre(card, window);

        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Colors.Transparent, Fill(chrome));
        Assert.Equal(0, clicks);
        Assert.All(card.GetVisualDescendants().OfType<TextBlock>(), t => Assert.Equal(Brush("Brush.InkSubtle"), Assert.IsType<ISolidColorBrush>(t.Foreground, exactMatch: false).Color));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_click_and_Enter_both_raise_Click() => await OnUi(() =>
    {
        var (window, card, _) = Show();
        var clicks = 0;
        card.Click += (_, _) => clicks++;
        var point = Centre(card, window);

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Assert.True(card.Focus(NavigationMethod.Tab));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, clicks);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Every_state_keeps_4_5_to_1_for_text_and_3_to_1_for_the_ring_and_the_selected_border() => await OnUi(() =>
    {
        var canvas = Brush("Brush.Canvas");
        var states = new[] { canvas, Brush("Brush.Surface2"), Brush("Brush.Surface3"), Brush("Brush.PrimarySoft") };

        Assert.All(states, fill => Assert.True(Contrast(Brush("Brush.Ink"), fill) >= 4.5, "Ink on " + fill));
        Assert.True(Contrast(Brush("Brush.PrimaryHover"), canvas) >= 3, "focus ring on canvas");
        Assert.True(Contrast(Brush("Brush.PrimaryHover"), Brush("Brush.PrimarySoft")) >= 3, "focus ring on the selected fill");
        Assert.True(Contrast(Brush("Brush.Primary"), canvas) >= 3, "selected border on canvas");
        return true;
    });
}
