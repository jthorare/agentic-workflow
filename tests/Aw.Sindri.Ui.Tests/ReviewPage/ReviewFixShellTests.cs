using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The fix-task sheet in the shell's sheet layer (review page spec §8.2, §6.2 "Sheets follow the app's rule"): a check opens it, closing returns focus.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewFixShellTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> OnPage()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await ShellWait.Until(shell, () => shell.ReviewPage.State == ReviewPageState.Ready, Token);
        return shell;
    }

    [Fact]
    public void The_fix_sheet_link_parses_and_prints_back()
    {
        Assert.True(DeepLink.TryParse("reviews/fix", out var link));

        Assert.Equal((DeepLinkKind.FixTaskSheet, "reviews/fix"), (link.Kind, link.ToString()));
    }

    [Fact]
    public Task Send_as_fix_task_on_a_failed_check_opens_the_sheet_over_the_page_prefilled() => UiThread.Run(async () =>
    {
        var shell = await OnPage();

        shell.ReviewPage.RequestCheckFix("ci-ui");

        Assert.True(shell.IsSheetOpen);
        Assert.True(shell.IsFixTaskSheet);
        Assert.True(shell.IsReviewPageShown);   // the sheet is over the page, the page stays
        Assert.StartsWith("Required check failed: Recipe change shows Ui tests ran.", shell.ReviewFix.Text, StringComparison.Ordinal);
        Assert.False(shell.IsSheetPlaceholderShown);
    }, Token);

    [Fact]
    public Task Closing_the_sheet_closes_it_and_sends_focus_to_the_check_it_came_from() => UiThread.Run(async () =>
    {
        var shell = await OnPage();
        shell.ReviewPage.RequestCheckFix("ci-ui");

        shell.ReviewFix.Close();

        Assert.False(shell.IsSheetOpen);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.Panel, "requiredChecks:ci-ui"), shell.ReviewPage.Focus);
    }, Token);

    [Fact]
    public Task The_sheet_reads_Send_as_fix_task_as_its_heading() => UiThread.Run(async () =>
    {
        var shell = await OnPage();

        shell.ReviewPage.RequestCheckFix("ci-ui");

        Assert.Equal("Send as fix task", shell.SheetTitle);
    }, Token);
}
