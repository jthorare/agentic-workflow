using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Agents tab's count and the tray line follow the status' session counts (collector spec CU-1, CA-8; U11a T5).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentCountsTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Running()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _records.Write(A.Record());
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        return shell;
    }

    private static string TabCount(ShellViewModel shell) => shell.Navigation.First(n => n.Page == PageId.Agents).Count;

    [Fact]
    public Task Tab_count_is_empty_before_the_first_status() => UiThread.Run(() =>
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());

        Assert.Equal("", TabCount(shell));
        Assert.Equal("", shell.Agents.NavCount);
        return Task.CompletedTask;
    }, Token);

    [Fact]
    public Task Tab_count_is_status_sessions_live() => UiThread.Run(async () =>
    {
        _server.Current = A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0) };
        var shell = await Running();
        await ShellWait.Until(shell, () => TabCount(shell).Length > 0, Token);

        Assert.Equal("3", TabCount(shell));
        Assert.Equal("3", shell.Agents.NavCount);
    }, Token);

    [Fact]
    public Task Tab_count_is_empty_when_the_status_serves_no_sessions() => UiThread.Run(async () =>
    {
        var shell = await Running();

        Assert.Equal("", TabCount(shell));
    }, Token);

    [Fact]
    public Task Tab_count_is_blank_while_the_control_plane_is_not_running_3710() => UiThread.Run(async () =>
    {
        _server.Current = A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0) };
        var shell = await Running();
        await ShellWait.Until(shell, () => TabCount(shell) == "3", Token);

        _records.Remove();
        await ShellWait.Until(shell, () => shell.State == ShellState.NotRunning, Token);

        Assert.Equal(("", ""), (TabCount(shell), shell.Agents.NavCount));
    }, Token);

    [Fact]
    public Task A_status_that_changes_the_figures_is_said_while_the_page_is_shown_and_not_otherwise() => UiThread.Run(async () =>
    {
        _server.Current = A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0) };
        var shell = await Running();
        await ShellWait.Until(shell, () => TabCount(shell) == "3", Token);
        var url = new Uri("http://127.0.0.1:7410/");

        shell.Health.Show(url, A.Status() with { Sessions = new SessionCountsView(Live: 4, Asking: 1, Problems: 0) });
        Assert.False(shell.Agents.Voice.IsAnnouncing);   // the page is not shown: the tab count moved, nothing is said

        shell.GoTo(PageId.Agents);
        shell.Health.Show(url, A.Status() with { Sessions = new SessionCountsView(Live: 4, Asking: 2, Problems: 0) });
        Assert.Equal(Labels.AgentsCounts(4, 2), shell.Agents.Voice.Announcement);
    }, Token);

    [Fact]
    public Task A_status_with_the_same_figures_says_nothing() => UiThread.Run(async () =>
    {
        _server.Current = A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0) };
        var shell = await Running();
        await ShellWait.Until(shell, () => TabCount(shell) == "3", Token);
        shell.GoTo(PageId.Agents);
        var before = shell.Agents.Voice.Announcement;

        shell.Health.Show(new Uri("http://127.0.0.1:7410/"), A.Status() with { Sessions = new SessionCountsView(Live: 3, Asking: 1, Problems: 0) });

        Assert.Equal(before, shell.Agents.Voice.Announcement);
    }, Token);
}
