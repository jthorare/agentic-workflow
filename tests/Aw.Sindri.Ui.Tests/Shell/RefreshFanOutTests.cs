using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Reviews;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>One ledger event must not re-read every followed view one after the other, nor the views of pages nobody looks at (#2775); the badges keep moving.</summary>
[Lane(TestLane.Unit)]
public sealed class RefreshFanOutTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Home is open, the first full read of the views is done.
    private async Task<ShellViewModel> HomeAfterFirstRead()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Health.ViewReads > 0, Token);
        return shell;
    }

    [Fact]
    public Task Reviews_badge_moves_while_Home_is_open() => UiThread.Run(async () =>
    {
        var shell = await HomeAfterFirstRead();
        var tab = shell.Navigation.Single(n => n.Page == PageId.Reviews);
        await ShellWait.Until(shell, () => tab.Count.Length > 0, Token);
        var list = ReviewsFixture.Golden("some");
        list["waitingCount"] = 1;
        _server.ReviewsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var views = shell.Health.ViewReads;

        _server.Push(A.Event(71, "heartbeat.tick"));

        await ShellWait.Until(shell, () => shell.Health.ViewReads > views && string.Equals(tab.Count, "1", StringComparison.Ordinal), Token);
        Assert.True(shell.IsHome);
    }, Token);

    [Fact]
    public Task Events_with_the_Todo_panel_hidden_read_neither_the_From_elsewhere_line_nor_the_Linear_work_count() => UiThread.Run(async () =>
    {
        var shell = await HomeAfterFirstRead();
        shell.GoTo(PageId.System);   // Home, and with it the Todo panel, is not shown
        var views = shell.Health.ViewReads;
        await ShellWait.Until(shell, () => shell.Health.ViewReads > views, Token);   // one pass settles what leaving Home started
        (int Issues, int Since) Reads() => (_server.LinearQueries.Count, _server.LinearSinceAfters.Count);
        var (before, afterSettle) = (Reads(), shell.Health.ViewReads);

        _server.Push(A.Event(72, "heartbeat.tick"), A.Event(73, "heartbeat.tick"), A.Event(74, "heartbeat.tick"));

        await ShellWait.Until(shell, () => shell.Health.ViewReads > afterSettle, Token);
        Assert.Equal(before, Reads());
    }, Token);

    // The Linear changes come from the stored context data, which the context source sync refreshes and records as 'context.sources.synced': the count moves on that event with the panel hidden.
    [Fact]
    public Task A_Linear_change_still_moves_the_new_count_while_the_Todo_panel_is_hidden() => UiThread.Run(async () =>
    {
        var shell = await HomeAfterFirstRead();
        await ShellWait.Until(shell, () => shell.Linear.NewCount > 0, Token);
        shell.GoTo(PageId.System);
        _server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "empty");

        _server.Push(A.Event(75, "context.sources.synced"));

        await ShellWait.Until(shell, () => shell.Linear.NewCount == 0, Token);
        Assert.False(shell.IsHome);
    }, Token);
}
