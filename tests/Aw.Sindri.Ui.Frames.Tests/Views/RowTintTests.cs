using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The tints the view model computes are drawn (review run 1, K15 / arch-1): a failure keeps a steady tint, a calm row a subtle one, a new row slides in (180 ms) unless motion is reduced.</summary>
[Lane(TestLane.Integration)]
public sealed class RowTintTests : FrameHost
{
    private static Border FrameOf(Window window, string id) =>
        window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible && r.RowId == id)
            .SelectMany(r => r.GetVisualDescendants().OfType<Border>()).First(b => b.Name == "RowFrame");

    private static IEnumerable<IStyle> All(IStyle style) => new[] { style }.Concat(style.Children.SelectMany(All));

    [Fact]
    public Task A_failure_is_tinted_steadily_a_calm_row_subtly_and_a_seen_pulse_row_not_at_all() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Failure(), NeedsYouRows.Profile(), NeedsYouRows.Park()], ""));
        shell.MarkAllSeen();
        Settle(window);

        Assert.Contains("steady", FrameOf(window, "failure:service:index").Classes, StringComparer.Ordinal);
        Assert.Contains("subtle", FrameOf(window, "profile:drift").Classes, StringComparer.Ordinal);
        Assert.Empty(FrameOf(window, "park:ITM-01JC8").Classes.Intersect(["steady", "subtle", "pulsing"], StringComparer.Ordinal));
        Frame(window, "needs-you-tints");
    });

    [Fact]
    public Task A_row_that_arrives_after_the_first_list_slides_in_and_not_when_motion_is_reduced() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Failure()], ""));
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Failure(), NeedsYouRows.Park()], ""));
        Settle(window);

        Assert.Contains("slides-in", FrameOf(window, "park:ITM-01JC8").Classes, StringComparer.Ordinal);
        Assert.DoesNotContain("slides-in", FrameOf(window, "failure:service:index").Classes, StringComparer.Ordinal);
        var slide = All(Application.Current!.Styles).OfType<Style>().First(s => s.Selector?.ToString()?.Contains("slides-in", StringComparison.Ordinal) == true);
        var animation = Assert.IsType<Avalonia.Animation.Animation>(Assert.Single(slide.Animations));
        Assert.Equal(TimeSpan.FromMilliseconds(180), animation.Duration);

        Motion.Set(reduce: true);
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Failure(), NeedsYouRows.Park(), NeedsYouRows.Park("park:ITM-NEW")], ""));
        Settle(window);
        Assert.DoesNotContain("slides-in", FrameOf(window, "park:ITM-NEW").Classes, StringComparer.Ordinal);
    });
}
