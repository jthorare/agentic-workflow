using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The coverage strip under the headless platform: four bars, the sentence, the flyout rows, and a name that starts with what it shows.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class CoverageStripTests
{
    private static readonly CoverageArea[] _areas =
    [
        new("Tickets and decisions", CoverageLevel.Searched, "searched"),
        new("Product", CoverageLevel.Searched, "searched"),
        new("Code", CoverageLevel.Limited, "limited", "The code index isn't built yet."),
        new("Function names", CoverageLevel.NotAvailable, "not available"),
    ];

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CoverageStripTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, CoverageStrip Strip) Show()
    {
        var strip = new CoverageStrip { Areas = _areas, Lead = "3 of 4 areas searched.", Rest = "Code is limited." };
        var window = new Window { Content = strip, Width = 500, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, strip);
    }

    [Fact]
    public async Task There_is_one_34_by_6_bar_per_area_and_the_sentence_beside_them() => await OnUi(() =>
    {
        var (window, strip) = Show();

        var bars = strip.GetVisualDescendants().OfType<Border>().Where(b => b.Width == 34 && b.Height == 6).ToList();
        var words = strip.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        Assert.Equal(4, bars.Count);
        Assert.Equal(["3 of 4 areas searched.", "Code is limited."], words);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Only_the_limited_bar_is_hatched_and_the_searched_ones_are_Ink() => await OnUi(() =>
    {
        var (window, strip) = Show();

        var fills = strip.GetVisualDescendants().OfType<Border>().Where(b => b.Width == 34).Select(b => b.Background).ToList();

        Assert.True(fills[0] is Avalonia.Media.ISolidColorBrush);
        Assert.Equal(fills[0], fills[1]);
        Assert.True(fills[2] is Avalonia.Media.DrawingBrush);
        Assert.NotEqual(fills[0], fills[3]);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_button_is_named_by_its_sentence_so_the_visible_words_come_first() => await OnUi(() =>
    {
        var (window, strip) = Show();

        Assert.Equal("3 of 4 areas searched. Code is limited.", AutomationProperties.GetName(strip));
        Assert.True(strip.Focusable);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_flyout_has_a_36_px_row_per_area_with_its_level_in_words() => await OnUi(() =>
    {
        var (window, strip) = Show();

        var rows = strip.FlyoutRows.Children.OfType<Grid>().ToList();

        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.Equal(36, r.Height));
        Assert.Equal(
            ["Tickets and decisions: searched", "Product: searched", "Code: limited. The code index isn't built yet.", "Function names: not available"],
            rows.Select(AutomationProperties.GetName),
            StringComparer.Ordinal);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Pressing_the_strip_opens_the_flyout() => await OnUi(() =>
    {
        var (window, strip) = Show();

        strip.Flyout!.ShowAt(strip);
        Dispatcher.UIThread.RunJobs();

        Assert.True(strip.Flyout.IsOpen);
        strip.Flyout.Hide();
        window.Close();
        return true;
    });

    [Fact]
    public async Task Changing_the_areas_redraws_the_bars_and_rows() => await OnUi(() =>
    {
        var (window, strip) = Show();

        strip.Areas = [_areas[0]];
        Dispatcher.UIThread.RunJobs();

        _ = Assert.Single(strip.GetVisualDescendants().OfType<Border>(), b => b.Width == 34);
        _ = Assert.Single(strip.FlyoutRows.Children);
        window.Close();
        return true;
    });

    // C9
    [Fact]
    public async Task A_change_of_theme_variant_rebuilds_the_bars() => await OnUi(() =>
    {
        var (window, strip) = Show();
        var before = strip.GetVisualDescendants().OfType<Border>().Where(b => b.Width == 34).ElementAt(2).Background;

        window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Dispatcher.UIThread.RunJobs();

        Assert.NotSame(before, strip.GetVisualDescendants().OfType<Border>().Where(b => b.Width == 34).ElementAt(2).Background);
        window.Close();
        return true;
    });
}
