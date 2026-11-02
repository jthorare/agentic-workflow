using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A pulse or a stale mark never fades the row's text (review run 1, K11 / a11y-6; WCAG 1.4.3): the tint moves, the text does not.</summary>
[Lane(TestLane.Integration)]
public sealed class RowContrastTests : FrameHost
{
    private static IEnumerable<IStyle> All(IStyle style) => new[] { style }.Concat(style.Children.SelectMany(All));

    private static IEnumerable<Style> StylesFor(string selectorPart) =>
        All(Application.Current!.Styles).OfType<Style>().Where(s => s.Selector?.ToString()?.Contains(selectorPart, StringComparison.Ordinal) == true);

    [Fact]
    public Task The_pulse_animates_the_background_and_never_the_opacity() => OnUi(async () =>
    {
        _ = await Connected();

        var animated = StylesFor("pulsing").SelectMany(s => s.Animations).OfType<Avalonia.Animation.Animation>().SelectMany(a => a.Children).SelectMany(k => k.Setters.OfType<Setter>()).Select(s => s.Property).Distinct().ToList();

        Assert.Equal([Border.BackgroundProperty], animated);
    });

    [Fact]
    public Task Stale_rows_keep_their_text_at_full_opacity() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);
        shell.NeedsYou.Rows[0].SetContext(stale: true, rebuilding: false, selected: false);
        shell.Strip.SetStale(stale: true);
        Settle(window);

        var rows = window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible).ToList();
        var stale = rows.First(r => r.GetVisualDescendants().OfType<Border>().Any(b => b.Classes.Contains("stale")));

        Assert.All(stale.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("needs-you-row")), b => Assert.Equal(1.0, b.Opacity));
        Assert.DoesNotContain(StylesFor("stale").SelectMany(s => s.Setters.OfType<Setter>()), s => s.Property == Visual.OpacityProperty);
    });
}
