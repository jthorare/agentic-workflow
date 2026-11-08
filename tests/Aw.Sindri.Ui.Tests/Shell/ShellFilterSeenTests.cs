using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Home filter shows fewer rows, not a shorter list (review run 1, K4 / corr-2): seen state is kept against the whole feed.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellFilterSeenTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task A_seen_row_stays_seen_across_G_D_on_and_off_and_in_the_preference_file() => UiThread.Run(async () =>
    {
        _records.Write(A.Record());
        var prefs = new Preferences(_store);
        var shell = new ShellViewModel(_records, _server, _clock, prefs, new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, Token);
        }

        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Park(), NeedsYouRows.Decision()], ""));
        shell.MarkAllSeen();
        var pulses = shell.NeedsYou.Seen.HeaderPulses;

        shell.GoTo(PageId.Home, HomeFilter.Decisions);
        shell.GoTo(PageId.Home);

        Assert.Equal([RowTint.None, RowTint.None], [.. shell.NeedsYou.Rows.Select(r => r.Tint)]);
        Assert.Equal(pulses, shell.NeedsYou.Seen.HeaderPulses);
        await prefs.Flush();
        Assert.Contains("park:ITM-01JC8", (await prefs.Load(Token)).SeenIds, StringComparer.Ordinal);
    }, Token);
}
