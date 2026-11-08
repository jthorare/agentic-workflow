using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Agents.Sheet;

/// <summary>The session sheet in the shell (U11a): the deep link claims the sheet from the placeholder, a row's request opens it and Esc knows the row, and it re-reads only while open.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSessionSheetTests
{
    private const string Ref = "claude-code/aaaaaaaa-0000-4000-8000-000000000001";
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DeepLink Link(string text) => DeepLink.TryParse(text, out var link) ? link : throw new InvalidOperationException(text);

    private async Task<ShellViewModel> Shell(bool connect)
    {
        if (connect)
        {
            _records.Write(A.Record());
        }

        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        if (connect)
        {
            _ = shell.Run(Token);
            for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
            {
                await Task.Delay(5, Token);
            }
        }

        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    [Fact]
    public Task Deep_link_session_opens_the_agents_page_and_claims_the_sheet() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: false);

        shell.Navigate(Link("session/" + Ref));

        Assert.Equal((PageId.Agents, true, false, Ref, FramePhase.Loading), (shell.CurrentPage, shell.IsSessionSheet, shell.IsSheetPlaceholderShown, shell.AgentSheet.Ref, shell.AgentSheet.Phase));
        Assert.Equal("Session " + Ref, shell.SheetName);
    }, Token);

    [Fact]
    public Task Another_kind_does_not_name_a_session() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: false);
        shell.Navigate(Link("session/" + Ref));

        shell.Navigate(Link("item/ITM-1"));

        Assert.False(shell.IsSessionSheet);
    }, Token);

    [Fact]
    public Task A_row_asking_to_open_opens_the_sheet_and_remembers_the_row_for_Esc() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: true);
        shell.GoTo(PageId.Agents);

        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(Link("session/" + Ref)));
        await Until(() => shell.AgentSheet.Phase == FramePhase.Shown);

        Assert.Equal((true, Ref, Ref), (shell.IsSessionSheet && shell.IsSheetShown, shell.OpenerSession, _server.LastSessionRef));
        shell.Navigate(Link("item/ITM-1"));
        Assert.Null(shell.OpenerSession);
    }, Token);

    [Fact]
    public Task The_edge_tab_reopens_the_session_sheet_without_an_opener() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: true);
        shell.Agents.RequestOpen(new SessionOpenRequestedEventArgs(Link("session/" + Ref)));
        shell.CloseSheet();
        Assert.Equal("Reopen session " + shell.AgentSheet.Heading, shell.EdgeTabName);
        Assert.DoesNotContain(Ref, shell.EdgeTabName, StringComparison.Ordinal);

        shell.ReopenSheet();

        Assert.Equal((true, true, null), (shell.IsSheetShown, shell.IsSessionSheet, shell.OpenerSession));
    }, Token);

    [Fact]
    public Task Re_reads_on_an_event_only_while_open() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: true);
        var url = shell.Health.ApiUrl()!;
        await shell.Health.RefreshViews(url, Token);
        Assert.Equal(0, _server.SessionCalls);   // nothing is open

        shell.Navigate(Link("session/" + Ref));
        await Until(() => shell.AgentSheet.Phase == FramePhase.Shown);
        var opened = _server.SessionCalls;
        await shell.Health.RefreshViews(url, Token);
        Assert.Equal(opened + 1, _server.SessionCalls);

        shell.CloseSheet();
        await shell.Health.RefreshViews(url, Token);
        Assert.Equal(opened + 1, _server.SessionCalls);   // closed to the edge tab: no read
    }, Token);

    [Fact]
    public Task The_sheet_has_the_catalogs_focus_control_once_connected() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: true);

        shell.Navigate(Link("session/" + Ref));
        await Until(() => shell.AgentSheet.Phase == FramePhase.Shown);

        Assert.Equal("Coming soon · sindri agents focus " + Ref, shell.AgentSheet.FocusLine);
    }, Token);
}
