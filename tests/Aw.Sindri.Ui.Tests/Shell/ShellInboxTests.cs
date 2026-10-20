using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Needs-you inbox in the shell (needs-you spec §2, slice 1): where it is entered, that Home stays the page behind it, and the way back.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellInboxTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty"), WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell(params NeedsYouEntry[] rows)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, Token);
        }

        shell.ShowNeedsYou(new NeedsYouFeed(rows, rows.Length == 0 ? "Nothing needs you" : ""));
        return shell;
    }

    [Fact]
    public Task Home_is_not_the_inbox_until_it_is_opened() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Park("desk:WFI-1"));

        Assert.False(shell.IsNeedsYouInbox);
    }, Token);

    [Fact]
    public Task Opening_the_inbox_keeps_Home_the_page_and_selects_the_first_row() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Failure(), NeedsYouRows.Park("desk:WFI-1"));

        shell.OpenNeedsYouInbox();

        Assert.True(shell.IsNeedsYouInbox);
        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.True(shell.IsHome);
        Assert.Equal("desk:WFI-1", shell.NeedsYou.SelectedId);
    }, Token);

    [Fact]
    public Task A_needs_you_link_opens_the_inbox_on_its_row() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Failure(), NeedsYouRows.Park("desk:WFI-1"));

        shell.Navigate(new DeepLink(DeepLinkKind.NeedsYou, "failure:service:index"));

        Assert.True(shell.IsNeedsYouInbox);
        Assert.Equal("failure:service:index", shell.NeedsYou.SelectedId);
        Assert.False(shell.IsSheetOpen);
    }, Token);

    [Fact]
    public Task A_link_to_a_row_the_list_does_not_have_still_opens_the_inbox_on_the_first_row() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Park("desk:WFI-1"));

        shell.Navigate(new DeepLink(DeepLinkKind.NeedsYou, "desk:gone"));

        Assert.True(shell.IsNeedsYouInbox);
        Assert.Equal("desk:WFI-1", shell.NeedsYou.SelectedId);
    }, Token);

    [Fact]
    public Task A_decision_row_still_opens_the_decision_sheet_for_now() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Decision());

        shell.Navigate(new DeepLink(DeepLinkKind.NeedsYou, "decision:DEC-1"));

        Assert.True(shell.IsDecisionSheet);
        Assert.False(shell.IsNeedsYouInbox);
    }, Token);

    [Fact]
    public Task Entering_a_row_from_Home_with_Enter_opens_the_inbox_on_it() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Failure(), NeedsYouRows.Park("desk:WFI-1"));
        shell.NeedsYou.Select("failure:service:index");

        var handled = shell.Press("Enter");

        Assert.True(handled);
        Assert.True(shell.IsNeedsYouInbox);
        Assert.Equal("failure:service:index", shell.NeedsYou.SelectedId);
    }, Token);

    [Fact]
    public Task J_and_K_walk_the_inbox_not_Homes_capped_list() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Failure(), NeedsYouRows.Park("desk:WFI-1"));
        shell.OpenNeedsYouInbox();

        _ = shell.Press("J");

        Assert.Equal("failure:service:index", shell.NeedsYou.SelectedId);
    }, Token);

    [Fact]
    public Task Leaving_returns_to_Home_with_its_panels() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Park("desk:WFI-1"));
        shell.OpenNeedsYouInbox();

        var left = shell.LeaveNeedsYouInbox();

        Assert.True(left);
        Assert.False(shell.IsNeedsYouInbox);
        Assert.True(shell.IsHome);
        Assert.False(shell.LeaveNeedsYouInbox());
    }, Token);

    [Fact]
    public Task Going_to_any_page_closes_the_inbox() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Park("desk:WFI-1"));
        shell.OpenNeedsYouInbox();

        shell.GoTo(PageId.Workflows);

        Assert.False(shell.IsNeedsYouInbox);
        shell.OpenNeedsYouInbox();
        shell.GoTo(PageId.Home);
        Assert.False(shell.IsNeedsYouInbox);
    }, Token);

    [Fact]
    public Task The_inbox_lists_every_row_after_Homes_decisions_filter_and_Home_gets_its_filter_back() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Decision(), NeedsYouRows.Park("desk:WFI-1"), NeedsYouRows.Failure());
        shell.GoTo(PageId.Home, HomeFilter.Decisions);
        _ = Assert.Single(shell.NeedsYou.Seen.Rows);

        shell.OpenNeedsYouInbox();
        Assert.Equal(["Your approvals", "Agent decisions", "Something failed"], shell.Inbox.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal("3 waiting, 0 runs", shell.Inbox.CountText);

        _ = shell.LeaveNeedsYouInbox();
        _ = Assert.Single(shell.NeedsYou.Seen.Rows);
    }, Token);

    [Fact]
    public Task Home_keeps_its_capped_panel_while_the_inbox_lists_every_row() => UiThread.Run(async () =>
    {
        var rows = Enumerable.Range(0, 8).Select(NeedsYouRows.Many).ToArray();
        var shell = await Shell(rows);

        shell.OpenNeedsYouInbox();

        Assert.Equal(NeedsYouViewModel.Cap, shell.NeedsYou.Rows.Count);
        Assert.Equal(8, shell.Inbox.Groups.Sum(g => g.Rows.Count));
    }, Token);
}
