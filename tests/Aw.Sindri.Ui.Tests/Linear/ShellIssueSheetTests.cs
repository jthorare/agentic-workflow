using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Linear issue sheet in the shell (Linear hub spec §3): the sindri://linear/KEY link, the openers (a Work → Linear row, a strip row, a review header chip), its names and Back.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellIssueSheetTests
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

    private static DeepLink Link(string text) => DeepLink.TryParse(text, out var link) ? link : throw new InvalidOperationException(text);

    [Fact]
    public void The_linear_deep_link_parses_prints_back_and_takes_only_a_key()
    {
        Assert.True(DeepLink.TryParse("linear/ENG-12", out var link));
        Assert.Equal((DeepLinkKind.Issue, "ENG-12", "linear/ENG-12"), (link.Kind, link.Subject, link.ToString()));
        Assert.False(DeepLink.TryParse("linear/ENG-12/comments", out _));
        Assert.False(DeepLink.TryParse("linear", out _));
    }

    [Fact]
    public Task The_deep_link_opens_the_sheet_and_reads_the_issue() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.Navigate(Link("linear/ENG-12"));
        await ShellWait.Until(shell, () => shell.IssueSheet.Phase == FramePhase.Shown, Token);

        Assert.Equal((true, true, false), (shell.IsSheetOpen, shell.IsIssueSheet, shell.IsSheetPlaceholderShown));
        Assert.Equal(("ENG-12", "Retry budget for the sync worker"), (shell.IssueSheet.Key, shell.IssueSheet.Title));
    }, Token);

    [Fact]
    public Task The_sheet_and_its_edge_tab_are_named_by_the_issue() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.Navigate(Link("linear/ENG-12"));
        var open = (shell.SheetName, shell.SheetTitle);
        shell.CloseSheet();

        Assert.Equal(("Issue ENG-12", "ENG-12"), open);
        Assert.Equal(("Reopen issue ENG-12", true), (shell.EdgeTabName, shell.IsEdgeTabShown));
    }, Token);

    [Fact]
    public Task A_Work_Linear_row_opens_the_sheet_and_Back_returns_to_the_page() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Work);

        shell.Linear.RequestIssue("ENG-12");
        var opened = (shell.IsSheetOpen, shell.IsIssueSheet, shell.SheetSubject);
        shell.Back();

        Assert.Equal((true, true, "ENG-12"), opened);
        Assert.Equal((false, PageId.Work), (shell.IsSheetOpen, shell.CurrentPage));
    }, Token);

    [Fact]
    public Task A_review_headers_issue_chip_opens_the_sheet() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.ReviewPage.OpenIssue("ENG-12");

        Assert.Equal((true, true, "ENG-12"), (shell.IsSheetOpen, shell.IsIssueSheet, shell.SheetSubject));
    }, Token);

    [Fact]
    public Task The_PR_chip_opens_the_review_page() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("linear/ENG-12"));
        await ShellWait.Until(shell, () => shell.IssueSheet.Phase == FramePhase.Shown, Token);

        shell.IssueSheet.OpenPr();

        Assert.Equal((false, PageId.Reviews, true), (shell.IsSheetOpen, shell.CurrentPage, shell.IsReviewPageShown));
    }, Token);

    [Fact]
    public Task The_open_sheet_reads_again_on_the_events_the_Linear_view_does() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("linear/ENG-12"));
        await ShellWait.Until(shell, () => shell.IssueSheet.Phase == FramePhase.Shown, Token);
        var newer = LinearIssueSheetFixture.Golden("full");
        newer["title"] = "Retry budget, per hour";
        _server.LinearIssueAnswer = LinearIssueSheetFixture.Served(newer);

        await shell.Health.RefreshViews(shell.Health.ApiUrl()!, Token);

        await ShellWait.Until(shell, () => shell.IssueSheet.Title == "Retry budget, per hour", Token);
    }, Token);
}
