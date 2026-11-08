using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>The Reviews tab in the shell (review page spec §3, §6.1, §16 pick 5): the seventh tab, G V, the deep link, the badge and Home's line.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellReviewsTests
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

    private static NavEntry Tab(ShellViewModel shell) => shell.Navigation.Single(n => n.Page == PageId.Reviews);

    [Fact]
    public Task Reviews_is_the_seventh_tab_after_System() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        Assert.Equal(
            [PageId.Home, PageId.Projects, PageId.Work, PageId.Context, PageId.Agents, PageId.System, PageId.Reviews],
            shell.Navigation.Select(n => n.Page));
        Assert.Equal(("Reviews", "V"), (Tab(shell).Label, Tab(shell).Key));
    }, Token);

    [Fact]
    public Task G_V_opens_Reviews_and_reads_the_list() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        _ = shell.PressKey("G");
        Assert.True(shell.PressKey("V"));

        Assert.True(shell.IsReviews);
        await ShellWait.Until(shell, () => !shell.Reviews.IsLoading, Token);
        Assert.Equal(2, shell.Reviews.Groups.Count);
    }, Token);

    [Fact]
    public void The_reviews_deep_link_parses_and_prints_back()
    {
        Assert.True(DeepLink.TryParse("reviews", out var link));
        Assert.Equal((DeepLinkKind.Reviews, "reviews"), (link.Kind, link.ToString()));
    }

    [Fact]
    public Task The_badge_and_Homes_line_show_the_waiting_count_from_any_page() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        await ShellWait.Until(shell, () => Tab(shell).Count.Length > 0, Token);

        Assert.True(shell.IsHome);
        Assert.Equal(("2", "Reviews, 2 waiting on you"), (Tab(shell).Count, Tab(shell).AccessibleName));
        Assert.Equal("2 PRs waiting on you", shell.ReviewsWaitingLine);
    }, Token);

    [Fact]
    public Task Nothing_waiting_hides_the_badge_and_Homes_line() => UiThread.Run(async () =>
    {
        _server.ReviewsAnswer = RouteAnswer.Api("reviews", "empty");
        var shell = await Shell();

        await ShellWait.Until(shell, () => !shell.Reviews.IsLoading, Token);

        Assert.Equal((string.Empty, "Reviews"), (Tab(shell).Count, Tab(shell).AccessibleName));
        Assert.Null(shell.ReviewsWaitingLine);
    }, Token);

    [Fact]
    public Task One_waiting_reads_singular() => UiThread.Run(async () =>
    {
        var list = ReviewsFixture.Golden("some");
        list["waitingCount"] = 1;
        _server.ReviewsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var shell = await Shell();

        await ShellWait.Until(shell, () => Tab(shell).Count.Length > 0, Token);

        Assert.Equal(("1 PR waiting on you", "Reviews, 1 waiting on you"), (shell.ReviewsWaitingLine, Tab(shell).AccessibleName));
    }, Token);
}
