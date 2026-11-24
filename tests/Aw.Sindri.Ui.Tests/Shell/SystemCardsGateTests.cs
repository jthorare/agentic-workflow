using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Code index and Context index cards read only while System is the page (#2775): nothing off it shows them or the headline they feed.</summary>
[Lane(TestLane.Unit)]
public sealed class SystemCardsGateTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Health.ViewReads > 0, Token);
        return shell;
    }

    private static bool CardsShown(ShellViewModel shell) => shell.Health.CodeIndex.Lines.Count > 0 && shell.Health.ContextIndex.Lines.Count > 0;

    private (int Code, int Context) Reads() => (_server.CodeIndexCalls, _server.IndexCalls);

    [Fact]
    public Task An_event_off_System_reads_neither_card() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var (before, views) = (Reads(), shell.Health.ViewReads);

        _server.Push(A.Event(70, "heartbeat.tick"));
        await ShellWait.Until(shell, () => shell.Health.ViewReads > views, Token);

        Assert.Equal(before, Reads());
    }, Token);

    [Fact]
    public Task Opening_System_reads_each_card_once() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var (code, context) = Reads();

        shell.GoTo(PageId.System);
        await ShellWait.Until(shell, () => CardsShown(shell), Token);

        Assert.Equal((code + 1, context + 1), Reads());
    }, Token);

    [Fact]
    public Task Every_event_reads_both_cards_while_System_is_shown() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.GoTo(PageId.System);
        await ShellWait.Until(shell, () => CardsShown(shell), Token);
        var ((code, context), views) = (Reads(), shell.Health.ViewReads);

        _server.Push(A.Event(71, "heartbeat.tick"));
        await ShellWait.Until(shell, () => shell.Health.ViewReads > views, Token);

        Assert.Equal((code + 1, context + 1), Reads());
    }, Token);

    [Fact]
    public Task Opening_System_again_reads_each_card_again() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var (code, context) = Reads();

        shell.GoTo(PageId.System);
        shell.GoTo(PageId.Home);
        shell.GoTo(PageId.System);

        Assert.Equal((code + 2, context + 2), Reads());
    }, Token);

    [Fact]
    public Task Leaving_System_cancels_a_read_in_flight() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        _server.CodeIndexHold = new TaskCompletionSource();
        shell.GoTo(PageId.System);
        var token = _server.CodeIndexToken;
        Assert.False(token.IsCancellationRequested);

        shell.GoTo(PageId.Home);

        Assert.True(token.IsCancellationRequested);
    }, Token);

    [Fact]
    public Task A_read_cancelled_by_leaving_shows_nothing_when_it_answers_late() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var hold = _server.CodeIndexHold = new TaskCompletionSource();
        _server.IgnoreCancellation = true;
        using var cancel = new CancellationTokenSource();
        var read = shell.Health.CodeIndex.Refresh(shell.Health.ApiUrl()!, cancel.Token);
        await cancel.CancelAsync();

        hold.SetResult();
        await read;

        Assert.Empty(shell.Health.CodeIndex.Lines);
    }, Token);

    [Fact]
    public Task A_late_answer_does_not_overwrite_a_newer_one() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var (card, url) = (shell.Health.CodeIndex, shell.Health.ApiUrl()!);
        var hold = _server.CodeIndexHold = new TaskCompletionSource();
        var older = card.Refresh(url, Token);
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, """{"repos":[]}""");
        await card.Refresh(url, Token);
        Assert.Empty(card.Lines);

        hold.SetResult();
        await older;

        Assert.Empty(card.Lines);
    }, Token);

    [Fact]
    public Task Opening_System_while_Not_running_reads_neither_card() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var before = Reads();
        _ = shell.Health.NotRunning("stopped");

        shell.GoTo(PageId.System);

        Assert.Equal(before, Reads());
    }, Token);
}
