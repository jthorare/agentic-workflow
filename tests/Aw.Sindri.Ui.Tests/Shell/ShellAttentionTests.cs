using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The shell's side of the attention summary (U3' T5): title, summary and live region follow the list and the state.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellAttentionTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouFeed Feed(params NeedsYouEntry[] rows) => new(rows, "") { Counts = new HomeCounts(Running: 2, WithYou: 0, Queued: 1) };

    private async Task<ShellViewModel> Shell(bool running)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        if (running)
        {
            _records.Write(A.Record());
            _ = shell.Run(Token);
            await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);

            Assert.Equal(ShellState.Normal, shell.State);
        }

        return shell;
    }

    [Fact]
    public Task Homes_header_counts_the_rows_Home_lists_while_the_title_and_badge_count_every_row() => UiThread.Run(async () =>
    {
        var shell = await Shell(running: true);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Decision(), NeedsYouRows.Failure()));
        shell.GoTo(PageId.Home, HomeFilter.Decisions);   // Home lists the decision only

        _ = Assert.Single(shell.NeedsYou.Rows);
        Assert.Equal("1 need you · 2 running · 0 with you · 1 queued", shell.Attention.SummaryLine);
        Assert.Equal(3, shell.Attention.Badge);
        Assert.Equal("(3) Needs you · Sindri", shell.WindowTitle);

        shell.GoTo(PageId.Home);

        Assert.Equal("3 need you · 2 running · 0 with you · 1 queued", shell.Attention.SummaryLine);
    }, Token);

    [Fact]
    public Task When_the_card_caps_its_rows_the_header_and_the_all_N_link_name_the_same_N() => UiThread.Run(async () =>
    {
        var shell = await Shell(running: true);

        shell.ShowNeedsYou(Feed([.. Enumerable.Range(1, 7).Select(NeedsYouRows.Many)]));

        Assert.Equal(NeedsYouViewModel.Cap, shell.NeedsYou.Rows.Count);
        Assert.StartsWith("7 need you", shell.Attention.SummaryLine, StringComparison.Ordinal);
        Assert.Equal(Labels.AllRows(7), shell.NeedsYou.OverflowText);
    }, Token);

    [Fact]
    public Task The_window_title_follows_the_needs_you_count() => UiThread.Run(async () =>
    {
        var shell = await Shell(running: true);
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal("Sindri", shell.WindowTitle);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));

        Assert.Equal("(2) Needs you · Sindri", shell.WindowTitle);
        Assert.Contains(changed, n => string.Equals(n, nameof(ShellViewModel.WindowTitle), StringComparison.Ordinal));

        shell.ShowNeedsYou(Feed());

        Assert.Equal("Sindri", shell.WindowTitle);
    }, Token);

    [Fact]
    public Task The_summary_is_the_trays_and_the_badges_source() => UiThread.Run(async () =>
    {
        var shell = await Shell(running: true);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Profile()));

        Assert.Equal((2, TrayState.Attention), (shell.Attention.Badge, shell.Attention.Tray.State));
        Assert.Equal("2 need you · 2 running · 0 with you · 1 queued", shell.Attention.SummaryLine);
    }, Token);

    [Fact]
    public Task Before_the_first_answer_the_tray_is_hollow_and_nothing_is_counted() => UiThread.Run(async () =>
    {
        var shell = await Shell(running: false);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park()));

        Assert.Equal(ShellState.Connecting, shell.State);
        Assert.Equal((0, TrayState.Stale, "Sindri"), (shell.Attention.Badge, shell.Attention.Tray.State, shell.WindowTitle));
    }, Token);

    [Fact]
    public Task The_live_region_says_N_now_need_you_once_for_each_rise() => UiThread.Run(async () =>
    {
        var shell = await Shell(running: true);
        Assert.Equal(Labels.Sindri, shell.NeedsYouAnnouncement);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));
        Assert.Equal("2 now need you", shell.NeedsYouAnnouncement);

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park()));
        Assert.Equal("2 now need you", shell.NeedsYouAnnouncement);   // a fall is the list's "<id> cleared"
    }, Token);
}
