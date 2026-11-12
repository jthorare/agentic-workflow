using Avalonia;
using Avalonia.Automation;
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
}
