using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Home;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The top bar's counts with Home as rows (#4227, #3733 frames "Home nav count = the All tab"): Home, Reviews, Work and Agents show their Home
/// tab's count, so the bar and the tabs never disagree on one screen. With the panels, each count is its own page's, as before.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ShellHomeNavCountsTests
{
    private static (PageId Page, HomeSplit Split)[] Counted =>
        [(PageId.Home, HomeSplit.All), (PageId.Reviews, HomeSplit.Reviews), (PageId.Work, HomeSplit.Work), (PageId.Agents, HomeSplit.Agents)];

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

    private static string Nav(ShellViewModel shell, PageId page) => shell.Navigation.First(n => n.Page == page).Count;

    private static string Tab(ShellViewModel shell, HomeSplit split) => shell.HomeRows.Tabs.First(t => t.Split == split).CountText;

    // What each page's own count is with the panels: Home has none, Reviews its waiting count (none at 0), Work the queue's total, Agents the live sessions.
    private static string[] OwnCounts(ShellViewModel shell) =>
    [
        string.Empty,
        shell.Reviews.WaitingCount > 0 ? shell.Reviews.WaitingCount.ToString(CultureInfo.InvariantCulture) : string.Empty,
        shell.Queue.IsLoading ? string.Empty : shell.Queue.Total.ToString(CultureInfo.InvariantCulture),
        shell.Agents.NavCount,
    ];

    [Fact]
    public Task With_Home_as_rows_each_nav_count_is_its_Home_tabs_count() => UiThread.Run(async () =>
    {
        var shell = await Connected(UiPreferences.HomeRows);
        await ShellWait.Until(shell, () => shell.HomeRows.Groups.Count > 0, Token);

        Assert.Equal(["6", "3", "0", "2"], Counted.Select(c => Tab(shell, c.Split)), StringComparer.Ordinal);
        Assert.Equal(["6", "3", "0", "2"], Counted.Select(c => Nav(shell, c.Page)), StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task With_the_panels_each_nav_count_stays_its_own_pages_count() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        Assert.Equal(OwnCounts(shell), Counted.Select(c => Nav(shell, c.Page)), StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task The_switch_moves_the_nav_counts_to_the_tabs_and_back() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        await shell.ShowHomeAsRows(rows: true);
        Assert.Equal(Counted.Select(c => Tab(shell, c.Split)), Counted.Select(c => Nav(shell, c.Page)), StringComparer.Ordinal);

        await shell.ShowHomeAsRows(rows: false);
        Assert.Equal(OwnCounts(shell), Counted.Select(c => Nav(shell, c.Page)), StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task A_pages_own_count_that_changes_under_the_rows_does_not_reach_the_bar_until_the_panels_are_back() => UiThread.Run(async () =>
    {
        var shell = await Connected(UiPreferences.HomeRows);
        await ShellWait.Until(shell, () => shell.HomeRows.Groups.Count > 0, Token);

        await shell.Queue.Refresh(shell.Health.ApiUrl()!, Token);

        Assert.Equal("0", Nav(shell, PageId.Work));
    }, Token);
}
