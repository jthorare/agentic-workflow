using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>Leaving Reviews takes the review page and the edge tab off the screen (#3092): the window binds to the shell's flags, so a page change must raise them.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPageLeaveTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Running()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        return shell;
    }

    private static List<string?> Raised(ShellViewModel shell)
    {
        var raised = new List<string?>();
        shell.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    [Fact]
    public Task Leaving_reviews_hides_the_review_page() => UiThread.Run(async () =>
    {
        var shell = await Running();
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        await ShellWait.Until(shell, () => shell.ReviewPage.State == ReviewPageState.Ready, Token);
        Assert.True(shell.IsReviewPageShown);
        var raised = Raised(shell);

        shell.GoTo(PageId.Work);

        Assert.Contains(nameof(ShellViewModel.IsReviewPageShown), raised, StringComparer.Ordinal);
        Assert.False(shell.IsReviewPageShown);
    }, Token);

    [Fact]
    public async Task Leaving_the_page_a_sheet_was_closed_on_raises_the_edge_tab_flag()
    {
        var shell = await Running();
        Assert.True(DeepLink.TryParse("job/JOB-9", out var link));
        shell.Navigate(link);
        shell.CloseSheet();
        Assert.True(shell.IsEdgeTabShown);
        var raised = Raised(shell);

        shell.GoTo(shell.CurrentPage == PageId.Work ? PageId.Agents : PageId.Work);

        Assert.Contains(nameof(ShellViewModel.IsEdgeTabShown), raised, StringComparer.Ordinal);
        Assert.False(shell.IsEdgeTabShown);
    }
}
