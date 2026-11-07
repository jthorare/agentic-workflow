using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The review page in the window (review page spec §6.2 "Layout by width", §6.3; mockups review-a-1440, review-a-1000): the two layouts, the strip and Enter on a list row.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewPageViewTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnPage(double width)
    {
        var (window, shell) = await Connected(width: width, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready && Visible(window, "ReviewPanelColumn"), "the review page never showed");
        return (window, shell);
    }

    // While Reload reads again the strip has no parts yet, so this can't throw the way Named would.
    private static bool FirstStripPartFocused(Visual within) =>
        within.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "ReviewStripSegment")?.IsFocused == true;

    private static Point Origin(Control control, Visual within) => control.TranslatePoint(default, within) ?? throw new InvalidOperationException(control.Name);

    [Fact]
    public Task At_1440_the_panel_column_is_420_wide_beside_the_diff() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(1440);

        var column = Named<Control>(window, "ReviewPanelColumn");
        var diff = Named<Control>(window, "ReviewDiffPane");
        Assert.True(shell.ReviewPage.IsWide);
        Assert.Equal(420, column.Bounds.Width, precision: 0);
        Assert.True(Origin(diff, window).X >= Origin(column, window).X + column.Bounds.Width);   // beside, not under
        Assert.InRange(Math.Abs(Origin(column, window).Y - Origin(diff, window).Y), 0, 40);   // the two columns start at about the same height
    });

    [Fact]
    public Task At_1000_the_panels_stack_above_the_diff_with_only_required_checks_open() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(1000);

        var column = Named<Control>(window, "ReviewPanelColumn");
        var diff = Named<Control>(window, "ReviewDiffPane");
        Assert.False(shell.ReviewPage.IsWide);
        Assert.True(Origin(diff, window).Y >= Origin(column, window).Y + column.Bounds.Height);   // under the panels, in one scroll
        Assert.Equal(["requiredChecks"], shell.ReviewPage.Panels.Where(p => !p.IsCollapsed).Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal(1, window.GetVisualDescendants().OfType<Control>().Count(c => c.Name == "ReviewPanelBody" && c.IsEffectivelyVisible));
    });

    [Fact]
    public Task The_strip_draws_one_segment_per_served_part_and_a_press_opens_its_panel() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(1000);
        var segments = window.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "ReviewStripSegment" && b.IsEffectivelyVisible).ToList();
        Assert.Equal(4, segments.Count);

        segments[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));   // "1 risky file"

        await Until(window, () => !shell.ReviewPage.Panels.Single(p => string.Equals(p.Id, "risk", StringComparison.Ordinal)).IsCollapsed, "the risk panel never opened");
    });

    [Fact]
    public Task The_moving_line_has_a_Reload_button_that_reads_again_and_focuses_the_first_strip_part() => OnUi(async () =>
    {
        Server.ReviewPageAnswer = RouteAnswer.Api("reviews-pr", "moving");
        var (window, shell) = await OnPage(1440);
        var reads = Server.ReviewPageReads.Count;

        Named<Button>(window, "ReviewReload").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await Until(window, () => Server.ReviewPageReads.Count > reads && shell.ReviewPage.State == ReviewPageState.Ready && FirstStripPartFocused(window), "Reload never read again and focused the strip");
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.StripFirst), shell.ReviewPage.Focus);
    });

    [Fact]
    public Task Enter_on_a_list_row_opens_that_PRs_review_page() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Reviews);
        await Until(window, () => !shell.Reviews.IsLoading, "the Reviews page never read the list");
        var row = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ReviewRowFrame" && b.IsEffectivelyVisible);
        _ = row.Focus(NavigationMethod.Tab);

        row.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = row });

        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == ReviewPageState.Ready, "Enter never opened the review page");
        Assert.Equal("acme/api#2071", shell.ReviewPage.Ref);
    });
}
