using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The review page's deep link 'reviews/&lt;owner&gt;/&lt;repo&gt;/&lt;number&gt;' (review page spec §6.2) and how Enter on a list row and the shell open it.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPageLinkTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        return shell;
    }

    [Fact]
    public void A_review_link_parses_to_the_review_kind_with_owner_repo_and_number_and_prints_back()
    {
        Assert.True(DeepLink.TryParse("reviews/acme/api/2071", out var link));

        Assert.Equal((DeepLinkKind.Review, "acme/api/2071", "reviews/acme/api/2071"), (link.Kind, link.Subject, link.ToString()));
        Assert.Equal(DeepLink.ForReview("acme/api", 2071), link);
    }

    [Theory]
    [InlineData("reviews/acme/api")]
    [InlineData("reviews/acme/api/x")]
    [InlineData("reviews/acme/api/0")]
    [InlineData("reviews/acme/api/2071/extra")]
    [InlineData("reviews/../api/2071")]
    [InlineData("reviews/acme/./2071")]
    [InlineData("reviews/ac me/api/2071")]
    public void A_review_link_with_a_missing_bad_or_extra_part_is_not_a_link(string text) =>
        Assert.False(DeepLink.TryParse(text, out _));

    [Fact]
    public void The_reviews_page_and_the_library_sheet_links_keep_their_own_kinds()
    {
        Assert.True(DeepLink.TryParse("reviews", out var page));
        Assert.True(DeepLink.TryParse("reviews/library", out var library));

        Assert.Equal((DeepLinkKind.Reviews, DeepLinkKind.LibrarySheet), (page.Kind, library.Kind));
    }

    [Fact]
    public async Task Enter_on_a_list_row_asks_for_that_PRs_review_link()
    {
        var list = new ReviewsPageViewModel(_server, new FakeTimeProvider(A.Noon));
        list.SetShown(shown: true);
        await list.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        var row = list.Groups[0].Sections[0].Rows[0];
        OpenRequestedEventArgs? asked = null;
        list.OpenRequested += (_, e) => asked = e;

        list.OpenRow(row);

        Assert.Equal("reviews/acme/api/2071", asked?.Link.ToString());
    }

    [Fact]
    public Task Navigating_to_a_review_link_opens_the_page_over_the_Reviews_list_and_reads_that_PR() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.Navigate(DeepLink.ForReview("acme/api", 2071));

        Assert.True(shell.IsReviews);
        Assert.True(shell.IsReviewPageShown);
        await ShellWait.Until(shell, () => shell.ReviewPage.State == ReviewPageState.Ready, Token);
        Assert.Equal("acme/api#2071", shell.ReviewPage.Ref);
        Assert.Equal([("acme/api", 2071)], _server.ReviewPageReads);
        Assert.False(shell.IsSheetOpen);   // the page is a page, not a sheet
    }, Token);

    [Fact]
    public Task Closing_the_review_page_shows_the_list_again() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(DeepLink.ForReview("acme/api", 2071));
        Assert.True(shell.IsReviewPageShown);

        shell.CloseReviewPage();

        Assert.False(shell.IsReviewPageShown);
        Assert.True(shell.IsReviews);
    }, Token);
}
