using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Issue #2777: opening an item from Work shows its title and state at once from the row, and the timeline fills while events keep coming.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellItemSheetFirstFrameTests
{
    private const string QueueRow = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301";

    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Queue.RunningRows.Count + shell.Queue.QueuedRows.Count > 0, Token);
        return shell;
    }

    [Fact]
    public Task Item_sheet_shows_the_rows_title_and_state_before_any_read_answers() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var row = shell.Queue.RunningRows.Concat(shell.Queue.QueuedRows).Single(r => string.Equals(r.Id, QueueRow, StringComparison.Ordinal));
        _server.ItemGates[QueueRow] = Gate();   // never released: no read answers

        shell.OpenItem(QueueRow);

        Assert.Equal((row.Title, row.StateWord, false), (shell.Item.Title, shell.Item.StateWord, shell.Item.IsLoading));
    }, Token);

    [Fact]
    public Task Item_sheet_fills_while_events_keep_coming_guard() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        _server.ItemDelay = TimeSpan.FromMilliseconds(30);   // a real answer takes time: each event's pass cancels the read in flight
        shell.OpenItem(A.NewItem);

        // An event every 50 ms of fake time; each read takes 30 ms. The timeline must show within 100 ms of the click.
        long sequence = 100;
        var filled = false;
        for (var step = 0; step < 2 && !filled; step++)
        {
            _server.Push(A.Event(++sequence));
            _clock.Advance(TimeSpan.FromMilliseconds(50));
            for (var spin = 0; spin < 4 && shell.Item.Timeline.Count == 0; spin++)
            {
                await Task.Delay(10, Token);
            }

            filled = shell.Item.Timeline.Count > 0;
        }

        Assert.True(filled);
    }, Token);
}
