using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>What each pre-API state shows (UI spec §4.1): the panels' visibility, the pill's words, and the sheet over the page.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellStateTests : IDisposable
{
    private readonly InlineUiContext _ui = new();
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    public void Dispose() => _ui.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Running(ShellState until)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Eventually(() => shell.State == until);
        return shell;
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)   // 2 s: the follower runs on the thread pool, so a loaded box needs room
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    private static (bool Mismatch, bool Chrome, bool NotRunning, bool Connecting, bool Unreadable, bool Page) Shown(ShellViewModel s) =>
        (s.IsVersionMismatch, s.IsChromeVisible, s.IsNotRunning, s.IsConnecting, s.IsUnreadable, s.IsPageVisible);

    [Fact]
    public async Task Connecting_shows_the_chrome_and_the_skeleton_only()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);

        Assert.Equal((false, true, false, true, false, false), Shown(shell));
    }

    [Fact]
    public async Task Not_running_shows_the_chrome_and_its_panel_in_place_of_the_page()
    {
        var shell = await Running(ShellState.NotRunning);

        Assert.Equal((false, true, true, false, false, false), Shown(shell));
    }

    [Fact]
    public async Task A_version_mismatch_is_a_full_window_panel_with_no_chrome_or_data()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };

        var shell = await Running(ShellState.VersionMismatch);

        Assert.Equal((true, false, false, false, false, false), Shown(shell));
        await Eventually(() => shell.Pill.Text == Labels.VersionMismatch);
        Assert.Equal(Labels.VersionMismatch, shell.Pill.Text);
        Assert.Equal([new HealthCell("This app", "API v1"), new HealthCell("Sindri", "API v2")], shell.Health.Detail.Versions);
    }

    [Fact]
    public async Task The_status_this_app_speaks_is_normal()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 1 };

        var shell = await Running(ShellState.Normal);

        await Eventually(() => shell.Pill.Text == "Running · updated 0s ago");
        Assert.Equal((false, true, false, false, false, true), Shown(shell));
        Assert.Equal("Running · updated 0s ago", shell.Pill.Text);
    }

    [Fact]
    public async Task The_pill_says_how_long_ago_the_views_were_read()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);

        _clock.Advance(TimeSpan.FromSeconds(5));

        await Eventually(() => shell.Pill.Text == "Running · updated 5s ago");
        Assert.Equal("Running · updated 5s ago", shell.Pill.Text);
        Assert.Equal(ShellState.Normal, shell.State);
    }

    [Fact]
    public async Task Unreadable_keeps_the_last_data_marked_stale_and_recovers_on_a_good_answer()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);

        _server.UnreadableStatus = true;
        _server.Push(A.Event(1));
        await Eventually(() => shell.State == ShellState.Unreadable);

        await Eventually(() => shell.Pill.Text == Labels.Unreadable + " · " + Labels.Stale);
        Assert.Equal((false, true, false, false, true, true), Shown(shell));
        Assert.Equal(Labels.Unreadable + " · " + Labels.Stale, shell.Pill.Text);
        Assert.Equal("pid 4242 · epoch 7", shell.Health.Lease);

        _server.UnreadableStatus = false;
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return shell.State == ShellState.Normal;
        });
    }

    [Fact]
    public async Task A_status_without_the_lease_is_not_running()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Running = false };

        var shell = await Running(ShellState.NotRunning);

        Assert.Equal(Labels.NoLease, shell.Reason);
        await Eventually(() => shell.Pill.Text == Labels.NotRunning);
        Assert.Equal(Labels.NotRunning, shell.Pill.Text);
    }

    [Fact]
    public async Task The_sheet_shows_only_over_a_visible_page()
    {
        var shell = await Running(ShellState.NotRunning);
        Assert.True(DeepLink.TryParse("decision/DEC-7", out var link));

        shell.Navigate(link);

        Assert.True(shell.IsSheetOpen);
        Assert.False(shell.IsSheetShown);
    }

    [Fact]
    public async Task The_edge_tab_goes_when_the_page_it_was_closed_on_is_left()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);
        Assert.True(DeepLink.TryParse("job/JOB-9", out var link));
        shell.Navigate(link);
        shell.CloseSheet();
        var closedOn = shell.CurrentPage;
        Assert.True(shell.IsEdgeTabShown);

        shell.GoTo(closedOn == PageId.Work ? PageId.Agents : PageId.Work);
        Assert.False(shell.IsEdgeTabShown);

        shell.GoTo(closedOn);
        Assert.True(shell.IsEdgeTabShown);
    }

    [Fact]
    public async Task The_edge_tab_reopens_the_last_subject()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);
        Assert.True(DeepLink.TryParse("job/JOB-9", out var link));
        shell.Navigate(link);
        shell.CloseSheet();
        Assert.True(shell.IsEdgeTabShown);

        shell.ReopenSheet();

        Assert.True(shell.IsSheetShown);
        Assert.False(shell.IsEdgeTabShown);
        Assert.Equal("JOB-9", shell.SheetSubject);
    }

    [Fact]
    public async Task The_top_bar_selection_follows_the_page_and_selecting_a_segment_goes_there()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);

        shell.SelectedNav = shell.Navigation[4];
        Assert.Equal((PageId.Context, true), (shell.CurrentPage, shell.IsContext));

        shell.GoTo(PageId.Settings);
        Assert.Null(shell.SelectedNav);
        Assert.True(shell.IsSettings);
    }

    // §4.1 Stale and §11.3: a stale pill is the ⌛ glyph and the word stale, still saying how old the views are, with a
    // Refresh beside it.
    private async Task<ShellViewModel> Stale()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Eventually(() => shell.State == ShellState.Stale);
        return shell;
    }

    // R3 (§4.1): while the views are stale (or unreadable, which keeps the last data) Home's strip says so: its name
    // gains "stale" after the health line, and the strip shows ⌛ and the word.
    [Fact]
    public async Task The_health_strip_is_marked_stale_with_the_views()
    {
        _records.Write(A.Record());
        var fresh = await Running(ShellState.Normal);
        Assert.False(fresh.Pill.IsDataStale);
        Assert.Equal(fresh.Health.HealthLine, fresh.Health.StripName);

        var stale = await Stale();
        await Eventually(() => stale.Pill.IsDataStale);
        Assert.True(stale.Pill.IsDataStale);
        Assert.Equal(stale.Health.HealthLine + " · " + Labels.Stale, stale.Health.StripName);
    }

    [Fact]
    public async Task Stale_is_the_hourglass_and_the_word_with_the_age_and_a_refresh()
    {
        var shell = await Stale();

        await Eventually(() => shell.Pill.Text == Labels.Stale + " · " + Labels.UpdatedAgo(ShellViewModel.StaleAfter));
        Assert.Equal((StateGlyph.Stale, Labels.Stale, Labels.UpdatedAgo(ShellViewModel.StaleAfter)), (shell.Pill.Glyph, shell.Pill.Word, shell.Pill.Age));
        Assert.Equal(Labels.Stale + " · " + Labels.UpdatedAgo(ShellViewModel.StaleAfter), shell.Pill.Text);
        Assert.True(shell.Pill.IsRefreshShown);
        Assert.True(shell.Pill.IsStale);
    }

    // H6 (corr-2, arch-1): a manual Refresh is a status read like the follower's: when the views are behind the sequence
    // of the last command, the sequence is used up and the status is read once more at once, so Refresh shows it.
    [Fact]
    public async Task Refresh_after_a_command_the_views_have_not_reached_shows_the_re_read()
    {
        var shell = await Stale();
        _server.Lag = ViewsLag.Behind;
        _ = await _server.RebuildViews(new Uri("http://127.0.0.1:7410/"), Token);

        var before = _server.StatusCalls;

        await shell.Health.Refresh(Token);

        Assert.Equal(ShellState.Normal, shell.State);
        Assert.Equal(before + 2, _server.StatusCalls);   // the read that carried the sequence, then the re-read without it
        Assert.Equal([43L, 0L], _server.StatusAfters.TakeLast(2));
    }

    [Fact]
    public async Task Refresh_reads_the_status_again_and_the_views_are_fresh()
    {
        var shell = await Stale();
        var before = _server.StatusCalls;

        await shell.Health.Refresh(Token);

        Assert.Equal(before + 1, _server.StatusCalls);
        Assert.Equal(ShellState.Normal, shell.State);
        await Eventually(() => shell.Pill.Word == Labels.Running && shell.Pill.Age == Labels.UpdatedAgo(TimeSpan.Zero));
        Assert.Equal((StateGlyph.Ok, Labels.Running, Labels.UpdatedAgo(TimeSpan.Zero)), (shell.Pill.Glyph, shell.Pill.Word, shell.Pill.Age));
        Assert.False(shell.Pill.IsRefreshShown);
    }

    // §4.1 "sending": while a refresh is in flight Refresh is disabled and a second click does nothing; "Sending…" shows
    // after 300 ms (so a quick answer never flashes it), and both clear when the answer comes.
    [Fact]
    public async Task While_a_refresh_is_in_flight_Refresh_is_disabled_and_a_second_click_does_nothing()
    {
        var shell = await Stale();
        var before = _server.StatusCalls;
        _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = shell.Health.Refresh(Token);
        Assert.False(shell.Health.CanRefresh);
        Assert.False(shell.Health.IsSending);
        var second = shell.Health.Refresh(Token);
        Assert.True(second.IsCompleted);
        Assert.Equal(before + 1, _server.StatusCalls);

        _clock.Advance(TimeSpan.FromMilliseconds(299));
        Assert.False(shell.Health.IsSending);
        Assert.Equal(Labels.Refresh, shell.Health.SendingAnnouncement);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await Eventually(() => shell.Health.IsSending);

        // R2 (§4.1): "Sending…" is the polite announcement, not a visible label.
        Assert.Equal(Labels.Sending, shell.Health.SendingAnnouncement);

        _server.StatusGate.SetResult();
        await first;
        Assert.True(shell.Health.CanRefresh);
        Assert.False(shell.Health.IsSending);
        Assert.Equal(Labels.Refresh, shell.Health.SendingAnnouncement);
        Assert.Equal(ShellState.Normal, shell.State);
    }

    // Review run 3 (julik-frontend-races-reviewer-1): a Refresh's answer that comes after the follow has decided a state
    // of its own (here: the record is gone, so Not running) is dropped. It never shows Running over Not running.
    [Fact]
    public async Task A_refresh_answered_after_the_follow_found_sindri_gone_is_dropped()
    {
        var shell = await Stale();
        _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = shell.Health.Refresh(Token);

        _records.Remove();
        _clock.Advance(HealthViewModel.RetryEvery);
        await Eventually(() => shell.State == ShellState.NotRunning);
        _server.StatusGate.SetResult();
        await refresh;

        Assert.Equal(ShellState.NotRunning, shell.State);
        Assert.True(shell.Health.CanRefresh);
    }

    // The same with Unreadable: the follow saw a refusal while the Refresh was in flight; the Refresh's later success
    // doesn't take the banner down.
    [Fact]
    public async Task A_refresh_answered_after_the_follow_saw_a_refusal_is_dropped()
    {
        var shell = await Stale();
        _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = shell.Health.Refresh(Token);

        _server.Refusal = A.NoTokenRefusal;
        _server.FailPing = false;
        _clock.Advance(HealthViewModel.PingEvery);
        await Eventually(() => shell.State == ShellState.Unreadable);
        _server.Refusal = null;
        _server.StatusGate.SetResult();
        await refresh;

        Assert.Equal(ShellState.Unreadable, shell.State);
    }

    // A Refresh answered by another process or epoch than the one the follow checked (a restart on the same port) isn't
    // the followed control plane: like a ping from someone else, it is dropped and the views stay stale.
    [Fact]
    public async Task A_refresh_answered_by_another_process_is_dropped()
    {
        var shell = await Stale();
        _server.Current = A.Status(pid: 5151, epoch: 9);

        await shell.Health.Refresh(Token);

        Assert.Equal(ShellState.Stale, shell.State);
    }

    // accessibility-tester-0 (docs/csharp/sindri-ui.md "LiveSetting on macOS"): the live region's text is never "" (Avalonia
    // #22312 crashes on a live element's name set to ""), it becomes "Sending…" only once IsSending (the view's live gate)
    // is on, and goes back to its resting text only once the gate is off, so the resting text is never spoken. Two cycles,
    // so the second "Sending…" is a change again.
    [Fact]
    public async Task The_sending_announcement_is_never_empty_and_changes_only_inside_the_live_gate()
    {
        var shell = await Stale();
        var seen = new List<(string Text, bool Live)>();
        var refreshable = new List<bool>();
        shell.Health.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HealthViewModel.SendingAnnouncement))
            {
                seen.Add((shell.Health.SendingAnnouncement, shell.Health.IsSending));
            }
            else if (e.PropertyName == nameof(HealthViewModel.CanRefresh))
            {
                refreshable.Add(shell.Health.CanRefresh);
            }
        };
        var resting = shell.Health.SendingAnnouncement;

        for (var cycle = 0; cycle < 2; cycle++)
        {
            _server.FailPing = true;
            _clock.Advance(ShellViewModel.StaleAfter);
            await Eventually(() => shell.State == ShellState.Stale);
            _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var refresh = shell.Health.Refresh(Token);
            _clock.Advance(HealthViewModel.SendingAfter);
            await Eventually(() => shell.Health.IsSending);
            _server.StatusGate.SetResult();
            await refresh;
        }

        Assert.False(string.IsNullOrEmpty(resting));
        Assert.NotEqual(Labels.Sending, resting, StringComparer.Ordinal);
        Assert.Equal([(Labels.Sending, true), (resting, false), (Labels.Sending, true), (resting, false)], seen);
        Assert.Equal([false, true, false, true], refreshable);
    }

    [Fact]
    public async Task A_failed_refresh_takes_a_click_again_and_a_quick_one_never_says_Sending()
    {
        var shell = await Stale();
        _server.FailStatus = true;
        var changed = new List<string>();
        shell.Health.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        await shell.Health.Refresh(Token);
        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(shell.Health.CanRefresh);
        Assert.False(shell.Health.IsSending);
        Assert.Contains(nameof(HealthViewModel.CanRefresh), changed, StringComparer.Ordinal);
        Assert.DoesNotContain(nameof(HealthViewModel.IsSending), changed, StringComparer.Ordinal);
        Assert.Equal(ShellState.Stale, shell.State);
    }

    // §4.1 Stale: after 3 failed refreshes the screen says "Sindri not responding: run sindri doctor". The sentence is
    // too long for the pill at the 900 px minimum, so it shows in full in a banner under the top bar; the pill keeps
    // ⌛ stale and its Refresh.
    [Fact]
    public async Task After_three_failed_refreshes_a_banner_says_Sindri_is_not_responding()
    {
        var shell = await Stale();
        _server.FailStatus = true;

        await shell.Health.Refresh(Token);
        await shell.Health.Refresh(Token);
        Assert.False(shell.IsNotRespondingBanner);
        await shell.Health.Refresh(Token);

        Assert.Equal(ShellState.Stale, shell.State);
        Assert.True(shell.IsNotRespondingBanner);
        await Eventually(() => shell.Pill.Word == Labels.Stale);
        Assert.Equal((StateGlyph.Stale, Labels.Stale), (shell.Pill.Glyph, shell.Pill.Word));
        Assert.True(shell.Pill.IsRefreshShown);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(shell.IsNotRespondingBanner);
    }

    // G1(c): "views behind" is an answer from a live Sindri (API §2.4), not a failed refresh.
    [Fact]
    public async Task A_refresh_that_hears_views_behind_does_not_count_toward_not_responding()
    {
        var shell = await Stale();
        _server.StatusError = new Error("SND-API-011", ErrorKind.Unavailable, "The views have not reached sequence 43 (fix: retry shortly)");

        for (var i = 0; i < 3; i++)
        {
            await shell.Health.Refresh(Token);
        }

        Assert.False(shell.IsNotRespondingBanner);
    }

    [Fact]
    public async Task The_not_responding_banner_clears_when_Sindri_answers_again()
    {
        var shell = await Stale();
        _server.FailStatus = true;
        for (var i = 0; i < 3; i++)
        {
            await shell.Health.Refresh(Token);
        }

        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        _server.FailStatus = false;
        await shell.Health.Refresh(Token);

        Assert.Equal(ShellState.Normal, shell.State);
        Assert.False(shell.IsNotRespondingBanner);
        Assert.Contains(nameof(ShellViewModel.IsNotRespondingBanner), changed, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Each_state_puts_its_glyph_before_its_word()
    {
        var connecting = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        Assert.Equal((StateGlyph.None, Labels.Connecting, string.Empty), (connecting.Pill.Glyph, connecting.Pill.Word, connecting.Pill.Age));
        // S1: no glyph while connecting, so the pill drops the glyph's slot and its word centres.
        Assert.False(connecting.Pill.HasGlyph);

        var down = await Running(ShellState.NotRunning);
        await Eventually(() => down.Pill.Word == Labels.NotRunning);
        Assert.Equal((StateGlyph.Down, Labels.NotRunning, string.Empty), (down.Pill.Glyph, down.Pill.Word, down.Pill.Age));
        Assert.True(down.Pill.IsDown);

        _records.Write(A.Record());
        _server.UnreadableStatus = true;
        var unreadable = await Running(ShellState.Unreadable);
        await Eventually(() => unreadable.Pill.Word == Labels.Unreadable);
        Assert.Equal((StateGlyph.Down, Labels.Unreadable, Labels.Stale), (unreadable.Pill.Glyph, unreadable.Pill.Word, unreadable.Pill.Age));
        Assert.True(unreadable.Pill.IsDown);

        _server.UnreadableStatus = false;
        var healthy = await Running(ShellState.Normal);
        await Eventually(() => healthy.Pill.Word == Labels.Running);
        Assert.Equal((StateGlyph.Ok, Labels.Running), (healthy.Pill.Glyph, healthy.Pill.Word));
        Assert.True(healthy.Pill.IsHealthy);
        Assert.True(healthy.Pill.HasGlyph);
    }

    // I7 (states.html Not running): the navigation is dimmed and disabled while nothing runs; it comes back when it does.
    [Fact]
    public async Task The_navigation_is_disabled_only_while_not_running()
    {
        Assert.True(new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard()).IsNavigationEnabled);
        var shell = await Running(ShellState.NotRunning);
        Assert.False(shell.IsNavigationEnabled);

        _records.Write(A.Record());
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return shell.State == ShellState.Normal;
        });
        Assert.True(shell.IsNavigationEnabled);
    }

    // ⌘, while not running (§12.3: Settings is local preferences, so it stays reachable): Settings shows over the Not
    // running card, and leaving it (Esc) returns to the card on the page it came from.
    [Fact]
    public async Task Settings_shows_over_the_not_running_card_and_leaving_it_returns_to_the_card()
    {
        var shell = await Running(ShellState.NotRunning);
        Assert.Equal((true, false), (shell.IsNotRunningPanel, shell.IsPageVisible));

        shell.GoTo(PageId.Settings);
        Assert.Equal((true, false, true), (shell.IsSettings, shell.IsNotRunningPanel, shell.IsPageVisible));
        Assert.False(shell.IsSheetShown);

        Assert.True(shell.LeaveSettings());
        Assert.Equal((PageId.Home, true, false), (shell.CurrentPage, shell.IsNotRunningPanel, shell.IsPageVisible));
        Assert.False(shell.LeaveSettings());
    }

    // Esc on Settings while connected keeps Settings: only the Not running card is a place to go back to.
    [Fact]
    public async Task Leaving_Settings_does_nothing_while_connected()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);
        shell.GoTo(PageId.Settings);

        Assert.False(shell.LeaveSettings());
        Assert.Equal(PageId.Settings, shell.CurrentPage);
    }
}
