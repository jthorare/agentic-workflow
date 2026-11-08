using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>What the Needs-you live region speaks, and when (review run 1, K9): once for each real event, flag before text, never "".</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSpokenTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouFeed Feed(params NeedsYouEntry[] rows) => new(rows, "") { Counts = new HomeCounts(Running: 2, WithYou: 0, Queued: 1) };

    private async Task<(ShellViewModel Shell, List<string> Spoken)> Listening()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, Token);
        }

        // What a screen reader would hear: each text set while the region is live (the flag changes first), never "".
        var spoken = new List<string>();
        shell.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ShellViewModel.NeedsYouAnnouncement), StringComparison.Ordinal) && shell.IsNeedsYouAnnouncing)
            {
                Assert.NotEmpty(shell.NeedsYouAnnouncement);
                spoken.Add(shell.NeedsYouAnnouncement);
            }
        };
        return (shell, spoken);
    }

    [Fact]
    public Task A_second_rise_to_the_same_count_is_spoken_again() => UiThread.Run(async () =>
    {
        var (shell, spoken) = await Listening();

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park()));
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure(), NeedsYouRows.Decision()));

        Assert.Equal(["2 now need you", "2 now need you", "3 now need you"], spoken);
    }, Token);

    [Fact]
    public Task A_cleared_selected_row_is_spoken() => UiThread.Run(async () =>
    {
        var (shell, spoken) = await Listening();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));
        spoken.Clear();
        Assert.True(shell.Press("J"));

        shell.ShowNeedsYou(Feed(NeedsYouRows.Failure()));

        Assert.Equal([Labels.Cleared("ITM-01JC8")], spoken);
    }, Token);

    [Fact]
    public Task One_feed_update_with_a_cleared_row_and_new_rows_is_one_sentence() => UiThread.Run(async () =>
    {
        var (shell, spoken) = await Listening();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));
        spoken.Clear();
        Assert.True(shell.Press("J"));

        shell.ShowNeedsYou(Feed(NeedsYouRows.Failure(), NeedsYouRows.Decision("decision:DEC-1"), NeedsYouRows.Decision("decision:DEC-2")));

        Assert.Equal(["ITM-01JC8 cleared, 3 now need you"], spoken);
    }, Token);

    [Fact]
    public void The_same_rows_back_after_the_views_were_away_are_not_announced_again()
    {
        var announcer = new AttentionAnnouncer();

        announcer.Observe(2);
        announcer.Observe(0, counting: false);   // NotRunning: the badge is 0, the rows are not gone
        announcer.Observe(2);

        Assert.Equal(1, announcer.Announcements);
    }
}
