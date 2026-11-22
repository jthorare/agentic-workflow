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
        await ShellWait.Until(shell, () => shell.Health.CodeIndex.Lines.Count > 0, Token);

        Assert.Equal((code + 1, context + 1), Reads());
    }, Token);

    [Fact]
    public Task Every_event_reads_both_cards_while_System_is_shown() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.GoTo(PageId.System);
        await ShellWait.Until(shell, () => shell.Health.CodeIndex.Lines.Count > 0, Token);
        var ((code, context), views) = (Reads(), shell.Health.ViewReads);

        _server.Push(A.Event(71, "heartbeat.tick"));
        await ShellWait.Until(shell, () => shell.Health.ViewReads > views, Token);

        Assert.Equal((code + 1, context + 1), Reads());
    }, Token);
}
