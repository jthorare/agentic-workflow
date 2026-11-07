using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The "Files changed" header of the diff pane with the fix-task sheet open at 1280 x 900 (#2228): the diff pane narrows, and the key hint must keep a gap from the heading and stay inside the pane (wrap or drop below), never touch the heading or run past the right edge.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewDiffHeaderLayoutTests : FrameHost
{
    private const double MinGap = 4;

    private async Task<(Window Window, ShellViewModel Shell)> OnPage(double width)
    {
        var (window, shell) = await Connected(width: width, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready && Visible(window, "ReviewDiffPane"), "the review page never showed");
        return (window, shell);
    }

    private static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    private static Control FirstLine(Window window) =>
        window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "ReviewDiffLine" && c.IsEffectivelyVisible && c.DataContext is ReviewDiffLineViewModel);

    private static Rect RectIn(Control pane, Control child) => new(child.TranslatePoint(default, pane) ?? default, child.Bounds.Size);

    [Fact]
    public Task With_the_fix_sheet_open_at_1280_the_diff_keys_hint_has_a_gap_and_is_not_cut_off() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(1280);
        var line = FirstLine(window);
        _ = line.Focus(NavigationMethod.Tab);
        Press(line, Key.C);
        await Until(window, () => shell.IsFixTaskSheet && shell.IsSheetOpen && Visible(window, "ReviewFixText"), "the fix sheet never opened");
        await Until(window, () => window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "ReviewDiffPane" && c.Bounds.Width > 0), "the diff pane never got a width");

        var pane = Named<Control>(window, "ReviewDiffPane");
        var texts = pane.GetVisualDescendants().OfType<TextBlock>().ToList();
        var heading = texts.First(t => t.Text == "Files changed");
        var hint = texts.First(t => t.Text?.Contains("j/k", StringComparison.Ordinal) == true);
        var h = RectIn(pane, heading);
        var k = RectIn(pane, hint);
        var detail = string.Create(CultureInfo.InvariantCulture, $"heading {h}, hint {k}, pane width {pane.Bounds.Width}");

        var sideBySideWithGap = k.Left >= h.Right + MinGap;
        var below = k.Top >= h.Bottom;
        Assert.True(sideBySideWithGap || below, $"the key hint touches or overlaps the heading (needs a 4 px gap on the row, or to sit below it): {detail}");
        Assert.True(k.Right <= pane.Bounds.Width, $"the key hint runs past the diff pane's right edge and is cut off: {detail}");
    });
}
