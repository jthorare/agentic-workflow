using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The workflow node card (#3234): title, type and a status chip, 200 by 64, and the tile pens in every state (hover, focus ring outside the selected ring, disabled, error).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class GraphNodeCardTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphNodeCardTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, GraphNodeCard Card) Show(Action<GraphNodeCard>? set = null)
    {
        var card = new GraphNodeCard { Title = "Ask the agent", Kind = "run_agent", StatusWord = "Running", StatusTone = "running", StatusGlyph = "◐", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        set?.Invoke(card);
        var window = new Window { Width = 400, Height = 300, Content = card };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, card);
    }

    private static Border Face(GraphNodeCard card) => card.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Card");

    private static List<Border> Rings(GraphNodeCard card) => [.. card.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "Ring")];

    private static Color Colour(IBrush? brush) => Assert.IsType<ISolidColorBrush>(brush, exactMatch: false).Color;

    [Fact]
    public async Task The_card_is_200_by_64_and_shows_the_title_the_type_and_the_status_chip() => await OnUi(() =>
    {
        var (window, card) = Show();

        var texts = card.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        var chip = card.GetVisualDescendants().OfType<StateChipView>().Single();

        Assert.Equal(200, card.Bounds.Width);
        Assert.Equal(64, card.Bounds.Height);
        Assert.Contains("Ask the agent", texts, StringComparer.Ordinal);
        Assert.Contains("run_agent", texts, StringComparer.Ordinal);
        Assert.Equal("Running", chip.Word);
        Assert.Equal("running", chip.Tone);
        Assert.Equal("◐", chip.Glyph);
        window.Close();
        return true;
    });

    [Fact]
    public async Task No_status_word_means_no_chip() => await OnUi(() =>
    {
        var (window, card) = Show(c => c.StatusWord = null);

        Assert.False(card.GetVisualDescendants().OfType<StateChipView>().Single().IsVisible);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_screen_reader_reads_the_title_then_the_type_then_the_status() => await OnUi(() =>
    {
        var (window, card) = Show();

        Assert.Equal("Ask the agent, run_agent, Running", AutomationProperties.GetName(card));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Selected_draws_the_inner_ring_and_keyboard_focus_a_separate_ring_outside_it() => await OnUi(() =>
    {
        var (window, card) = Show(c => c.IsSelected = true);
        var selectedOnly = Rings(card).Select(r => Colour(r.BorderBrush)).ToList();

        _ = card.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        var both = Rings(card);

        Assert.Equal(new[] { Color.Parse("#5e6ad2") }, selectedOnly);
        Assert.Equal(new[] { Color.Parse("#5e6ad2"), Color.Parse("#828fff") }, both.Select(r => Colour(r.BorderBrush)).ToList());
        Assert.True(both[1].Margin.Left < both[0].Margin.Left, "the focus ring sits outside the selected ring");
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_click_focus_draws_no_focus_ring_only_the_keyboard_does() => await OnUi(() =>
    {
        var (window, card) = Show();

        _ = card.Focus(NavigationMethod.Pointer);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Rings(card));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Hover_lifts_the_border_to_WireHi_on_the_Surface3_fill() => await OnUi(() =>
    {
        var (window, card) = Show();
        var restBorder = Colour(Face(card).BorderBrush);

        window.MouseMove(new Point(100, 32));
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(restBorder, Colour(Face(card).BorderBrush));
        Assert.Equal(Color.Parse("#8a8f98"), Colour(Face(card).BorderBrush));
        Assert.Equal(Color.Parse("#222326"), Colour(Face(card).Background));
        window.Close();
        return true;
    });

    [Fact]
    public async Task An_invalid_step_has_the_ErrorSoft_fill_and_a_2_px_Error_border() => await OnUi(() =>
    {
        var (window, card) = Show(c => c.IsInvalid = true);

        Assert.Equal(Color.Parse("#281415"), Colour(Face(card).Background));
        Assert.Equal(Color.Parse("#eb5757"), Colour(Face(card).BorderBrush));
        Assert.Equal(new Thickness(2), Face(card).BorderThickness);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_disabled_card_is_dashed_and_does_not_hover() => await OnUi(() =>
    {
        var (window, card) = Show(c => c.IsEnabled = false);
        var rest = Colour(Face(card).BorderBrush);

        window.MouseMove(new Point(100, 32));
        Dispatcher.UIThread.RunJobs();
        var dashed = card.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().Single(r => r.Name == "Dash");

        Assert.True(dashed.IsVisible);
        Assert.Equal(rest, Colour(Face(card).BorderBrush));
        window.Close();
        return true;
    });

    // What is DRAWN, not what the ring Border says (#3234): the card sits 16 px inside a panel, rendered at 4x, and a pixel on each ring's straight top edge is read back. The selected ring's
    // outer edge is 5 px above the card (gap 3 + 2 px stroke), its band 3 to 5 px above; the focus ring's outer edge is 8 px above (gap 6 + 2), its band 6 to 8 px above. Real drawing
    // needs AW_UI_FRAMES=1, as in RowGlyphSpecTests.
    private static readonly bool _draws = string.Equals(Environment.GetEnvironmentVariable("AW_UI_FRAMES"), "1", StringComparison.Ordinal);

    private static (byte R, byte G, byte B) DrawnAboveCard(Action<GraphNodeCard> set, double above, bool focus)
    {
        const int Scale = 4;
        const double Pad = 16;
        var card = new GraphNodeCard { Title = "Ask the agent", Kind = "run_agent", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        set(card);
        var host = new Border { Background = Brushes.Black, Padding = new Thickness(Pad), Child = card };
        var window = new Window { Content = host, Width = 232, Height = 96 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        if (focus)
        {
            _ = card.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }

        using var bitmap = new RenderTargetBitmap(new PixelSize(232 * Scale, 96 * Scale), new Vector(96 * Scale, 96 * Scale));
        bitmap.Render(host);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var read = WriteableBitmap.Decode(stream);
        using var frame = read.Lock();
        var bytes = new byte[232 * Scale * 4];
        var row = (int)((Pad - above) * Scale);
        Marshal.Copy(frame.Address + (row * frame.RowBytes), bytes, 0, bytes.Length);
        var at = (int)((Pad + 100) * Scale) * 4;   // the middle of the card's top edge
        window.Close();
        return (bytes[at + 2], bytes[at + 1], bytes[at]);
    }

    private static bool Near((byte R, byte G, byte B) got, string hex)
    {
        var want = Color.Parse(hex);
        return Math.Abs(got.R - want.R) <= 8 && Math.Abs(got.G - want.G) <= 8 && Math.Abs(got.B - want.B) <= 8;
    }

    [Fact]
    public async Task The_selected_ring_is_drawn_3_to_5_px_above_the_card_in_Primary() => await OnUi(() =>
    {
        if (!_draws)
        {
            return true;
        }

        var got = DrawnAboveCard(c => c.IsSelected = true, above: 4, focus: false);

        Assert.True(Near(got, "#5e6ad2"), $"the pixel 4 px above the card's top edge is ({got.R};{got.G};{got.B}), the selected ring is #5e6ad2");
        return true;
    });

    [Fact]
    public async Task The_focus_ring_is_drawn_6_to_8_px_above_the_card_in_PrimaryHover() => await OnUi(() =>
    {
        if (!_draws)
        {
            return true;
        }

        var got = DrawnAboveCard(_ => { }, above: 7, focus: true);

        Assert.True(Near(got, "#828fff"), $"the pixel 7 px above the card's top edge is ({got.R};{got.G};{got.B}), the focus ring is #828fff");
        return true;
    });
}
