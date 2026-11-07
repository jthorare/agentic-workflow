using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The review page spec's screenshot run (Ph7, §6.3; UI spec §18.2: one key frame per state, integration lane), on the fake API's hand-written
/// goldens: the Reviews list in each of its states, the review page in each served and refused state at 1440 and 1000, and the fix-task and
/// library sheets. Saved under frames/ as ph7-reviews-*.png and ph7-review-*.png with AW_UI_FRAMES=1; compare with the mockups in
/// docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design/mockups/screens/.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewFramesTests : FrameHost
{
    private const string Prefix = "ph7-";

    private static string Width(int width) => width.ToString(CultureInfo.InvariantCulture);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnList(string scenario, int width)
    {
        Server.ReviewsAnswer = RouteAnswer.Api("reviews", scenario);
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Reviews);
        await Until(window, () => !shell.Reviews.IsLoading, "the Reviews page never read the list");
        return (window, shell);
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnPage(RouteAnswer answer, int width, ReviewPageState state)
    {
        Server.ReviewPageAnswer = answer;
        var (window, shell) = await Connected(width: width, height: 900);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await Until(window, () => shell.IsReviewPageShown && shell.ReviewPage.State == state, "the review page never reached " + state);
        return (window, shell);
    }

    [Theory]
    [InlineData("some", 1440)]
    [InlineData("some", 1000)]
    [InlineData("empty", 1440)]
    [InlineData("no-login", 1440)]
    [InlineData("github-failing", 1440)]
    [InlineData("library-unreadable", 1440)]
    [InlineData("before-reviewers", 1440)]
    public Task The_Reviews_list_in_each_state(string scenario, int width) => OnUi(async () =>
    {
        var (window, _) = await OnList(scenario, width);

        Frame(window, "reviews-" + scenario + "-" + Width(width), Prefix);
    });

    [Theory]
    [InlineData("full", 1440)]
    [InlineData("full", 1000)]
    [InlineData("moving", 1440)]
    [InlineData("offline-cached", 1440)]
    [InlineData("own-pr", 1440)]
    [InlineData("panel-errors", 1440)]
    [InlineData("empty-panels", 1440)]
    [InlineData("capped", 1000)]
    public Task The_review_page_in_each_served_state(string scenario, int width) => OnUi(async () =>
    {
        var (window, _) = await OnPage(RouteAnswer.Api("reviews-pr", scenario), width, ReviewPageState.Ready);

        Assert.True(Visible(window, "ReviewPanelColumn"));
        Frame(window, "review-" + scenario + "-" + Width(width), Prefix);
    });

    [Theory]
    [InlineData("not-found", nameof(ReviewPageState.NotFound))]
    [InlineData("not-synced", nameof(ReviewPageState.NotSynced))]
    [InlineData("no-login", nameof(ReviewPageState.LoginPrompt))]
    public Task The_review_page_in_each_refused_state(string scenario, string state) => OnUi(async () =>
    {
        var (window, _) = await OnPage(RouteAnswer.Problem("reviews-pr", scenario), 1000, Enum.Parse<ReviewPageState>(state));

        Assert.True(Visible(window, "ReviewStateHeading"));
        Frame(window, "review-" + scenario + "-1000", Prefix);
    });

    [Fact]
    public Task The_review_page_offline_and_not_cached() => OnUi(async () =>
    {
        var answer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-REVIEW-503", "Offline, and this PR isn't cached yet");
        var (window, _) = await OnPage(answer, 1000, ReviewPageState.OfflineUncached);

        Assert.True(Visible(window, "ReviewStateHeading"));
        Frame(window, "review-offline-uncached-1000", Prefix);
    });

    [Fact]
    public Task The_fix_task_sheet_from_a_failed_check() => OnUi(async () =>
    {
        var (window, shell) = await OnPage(RouteAnswer.Api("reviews-pr", "full"), 1440, ReviewPageState.Ready);

        shell.ReviewPage.RequestCheckFix("ci-ui");
        await Until(window, () => shell.IsFixTaskSheet && Visible(window, "ReviewFixText"), "the fix sheet never opened");

        Frame(window, "review-fix-sheet-1440", Prefix);
    });

    [Fact]
    public Task The_library_errors_sheet() => OnUi(async () =>
    {
        Server.LibraryAnswer = RouteAnswer.Api("library", "with-errors");
        var (window, shell) = await OnList("some", 1440);

        Named<Button>(window, "ReviewsLibrary").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.IsLibrarySheet && !shell.Reviews.Library.IsLoading && Visible(window, "ReviewsLibrarySummary"), "the library sheet never showed");

        Frame(window, "reviews-library-sheet-1440", Prefix);
    });
}
