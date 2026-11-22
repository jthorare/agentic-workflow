using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The re-read map (U3' plan "Re-query map"): until the API serves what an event invalidates (P5, E-15) every event
/// re-reads every view the app shows, and needs-you always, because the badge and the tray show with the window closed.
/// These pass under that rule and keep passing when the app reads `invalidates` instead.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RequeryTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && _server.NeedsYouCalls > 0 && shell.Attention.Badge > 0, Token);
        return shell;
    }

    [Fact]
    public Task The_first_read_puts_the_rows_in_the_title_the_badge_and_the_summary() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        Assert.Equal(("(3) Needs you · Sindri", 3), (shell.WindowTitle, shell.Attention.Badge));
        Assert.Contains("3 need you", shell.Attention.SummaryLine, StringComparison.Ordinal);
    }, Token);

    [Theory]
    [InlineData("Home")]
    [InlineData("Settings")]
    public Task Every_event_rereads_needs_you_whatever_page_is_visible(string page) => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.GoTo(Enum.Parse<PageId>(page));
        var (before, reads) = (_server.NeedsYouCalls, shell.Health.ViewReads);

        _server.Push(A.Event(60, "heartbeat.tick"));

        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);
        Assert.True(_server.NeedsYouCalls > before);
    }, Token);

    [Theory]
    [InlineData("request.filed")]
    [InlineData("decision.recorded")]
    [InlineData("item.state-changed")]
    public Task Request_filed_decision_recorded_and_item_state_changed_reread_needs_you_decisions_queue_and_the_open_item(string type) => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.Navigate(new DeepLink(DeepLinkKind.Item, A.NewItem));
        await ShellWait.Until(shell, () => shell.Item.HasItem, Token);
        var (needsYou, decisions, items, reads) = (_server.NeedsYouCalls, _server.DecisionQueries.Count, _server.ItemCalls, shell.Health.ViewReads);
        _server.NeedsYouView = RouteAnswer.Api("needs-you", "after-decide");
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");

        _server.Push(A.Event(61, type));

        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);
        Assert.True(_server.NeedsYouCalls > needsYou && _server.DecisionQueries.Count > decisions && _server.ItemCalls > items);
        Assert.Equal((12, GoldenFiles.Node("needs-you", "after-decide")["rows"]!.AsArray().Count), (shell.Queue.Total, shell.Attention.Badge));
    }, Token);

    [Fact]
    public Task Service_ran_rereads_needs_you_and_services() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var (needsYou, services, reads) = (_server.NeedsYouCalls, _server.ServicesCalls, shell.Health.ViewReads);

        _server.Push(A.Event(62, "service.ran"));

        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);
        Assert.True(_server.NeedsYouCalls > needsYou && _server.ServicesCalls > services);
    }, Token);

    [Fact]
    public Task Profile_checked_rereads_needs_you() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var (before, reads) = (_server.NeedsYouCalls, shell.Health.ViewReads);

        _server.Push(A.Event(63, "profile.checked"));

        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);
        Assert.True(_server.NeedsYouCalls > before);
    }, Token);

    [Fact]
    public Task A_failed_needs_you_read_keeps_the_rows_the_badge_shows() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        _server.NeedsYouView = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-LEDGER-002", "the ledger is busy");
        var (before, reads) = (_server.NeedsYouCalls, shell.Health.ViewReads);
        _server.Push(A.Event(64, "service.ran"));

        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);
        Assert.True(_server.NeedsYouCalls > before);
        Assert.Equal(3, shell.Attention.Badge);
    }, Token);

    // #2775: the queue and needs-you are read side by side. When needs-you answers first it maps the old queue's groups; Home's and the
    // tray's totals must follow the queue once it answers, not wait for the next event.
    [Fact]
    public Task The_totals_follow_a_queue_that_answers_after_needs_you() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var queue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.QueueGate = queue;
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        _server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-nothing");
        var reads = shell.Health.ViewReads;

        _server.Push(A.Event(65, "item.state-changed"));
        await ShellWait.Until(shell, () => shell.Attention.Badge == 0, Token);   // needs-you has answered; the queue is still held

        Assert.Equal(Labels.TrayRunningItem(0), shell.Attention.Tray.RunningLabel);

        queue.SetResult();
        await ShellWait.Until(shell, () => shell.Health.ViewReads > reads, Token);

        Assert.Equal(Labels.TrayRunningItem(6), shell.Attention.Tray.RunningLabel);
    }, Token);
}
