using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Home as rows in the shell (home spec §7.1, U1): the preference picks rows or panels, and the rows are read page by page only while it asks for them.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellHomeRowsTests
{
    private const string Cursor = "gh:ready:jthorare/agentic-workflow#3101";

    private readonly InMemoryEndpointRecords _records = new();
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected(string? layout = null)
    {
        if (layout is not null)
        {
            await new Preferences(_store).Update(p => p with { HomeLayout = layout });
        }

        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_store), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && _server.NeedsYouCalls > 0, Token);
        return shell;
    }

    [Fact]
    public Task A_first_run_shows_the_panels_and_never_reads_the_rows() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        Assert.Equal((true, false, true), (shell.IsHomeShown, shell.IsHomeRowsShown, shell.IsPagesShown));
        Assert.Empty(_server.HomeReads);
    }, Token);

    [Fact]
    public Task The_rows_preference_shows_the_rows_in_place_of_the_panels() => UiThread.Run(async () =>
    {
        var shell = await Connected(UiPreferences.HomeRows);
        await ShellWait.Until(shell, () => shell.HomeRows.Groups.Count > 0, Token);

        Assert.Equal((false, true, false), (shell.IsHomeShown, shell.IsHomeRowsShown, shell.IsPagesShown));
        Assert.Equal(6, Assert.Single(shell.HomeRows.Groups).Rows.Count);
    }, Token);

    [Fact]
    public Task Another_page_shows_the_pages_again() => UiThread.Run(async () =>
    {
        var shell = await Connected(UiPreferences.HomeRows);

        shell.GoTo(PageId.System);

        Assert.Equal((false, false, true), (shell.IsHomeShown, shell.IsHomeRowsShown, shell.IsPagesShown));
    }, Token);

    [Fact]
    public Task A_read_follows_the_cursor_and_lists_each_row_once() => UiThread.Run(async () =>
    {
        _server.HomeFirstPage = RouteAnswer.Api("home", "after-cursor");
        _server.HomePagesAfter[Cursor] = RouteAnswer.Api("home", "fixture");

        var shell = await Connected(UiPreferences.HomeRows);
        await ShellWait.Until(shell, () => shell.HomeRows.Groups.Count > 0, Token);

        Assert.Equal([null, Cursor], _server.HomeReads.Take(2), StringComparer.Ordinal);
        Assert.Equal(6, Assert.Single(shell.HomeRows.Groups).Rows.Count);
    }, Token);

    [Fact]
    public Task The_switch_reads_the_rows_at_once_and_goes_back_to_the_panels() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        await shell.ShowHomeAsRows(rows: true);
        var asRows = (shell.IsHomeShown, shell.IsHomeRowsShown, shell.HomeRows.Groups.Count);
        await shell.ShowHomeAsRows(rows: false);

        Assert.Equal((false, true, 1), asRows);
        Assert.Equal((true, false, true), (shell.IsHomeShown, shell.IsHomeRowsShown, shell.IsPagesShown));
    }, Token);
}
