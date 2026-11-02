using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The attention summary (U3' T5): title, badge, tray, summary line and the live region's line.</summary>
[Lane(TestLane.Unit)]
public sealed class AttentionSummaryTests
{
    private static NeedsYouFeed Feed(params NeedsYouEntry[] rows) => new(rows, "") { Counts = new HomeCounts(Running: 3, WithYou: 1, Queued: 4) };

    private static AttentionSummary For(NeedsYouFeed? feed, ShellState state = ShellState.Normal) => AttentionSummary.For(feed, state, A.Noon);

    [Fact]
    public void Title_is_N_Needs_you_Sindri_and_plain_Sindri_at_zero()
    {
        Assert.Equal("(2) Needs you · Sindri", For(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure())).Title);
        Assert.Equal("Sindri", For(Feed()).Title);
        Assert.Equal("Sindri", For(feed: null).Title);
    }

    [Fact]
    public void The_summary_line_counts_the_rows_the_card_lists_while_the_title_and_badge_count_every_row()
    {
        var feed = Feed(NeedsYouRows.Park(), NeedsYouRows.Park(), NeedsYouRows.Park(), NeedsYouRows.Profile());   // the 4th stands for a setup row Home's setup line covers

        var listed = AttentionSummary.For(feed, ShellState.Normal, A.Noon, sessions: null, listed: 3);
        var all = AttentionSummary.For(feed, ShellState.Normal, A.Noon, sessions: null, listed: null);

        Assert.StartsWith("3 need you", listed.SummaryLine, StringComparison.Ordinal);
        Assert.StartsWith("4 need you", all.SummaryLine, StringComparison.Ordinal);
        Assert.Equal((4, "(4) Needs you · Sindri"), (listed.Badge, listed.Title));
    }

    [Fact]
    public void Badge_counts_every_row_including_calm()
    {
        Assert.Equal(3, For(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure(), NeedsYouRows.Profile())).Badge);
        Assert.Equal(1, For(Feed(NeedsYouRows.Profile())).Badge);
        Assert.Equal(0, For(Feed()).Badge);
    }

    [Fact]
    public void Tray_is_attention_when_any_pulse_or_colour_row_exists()
    {
        var pulse = For(Feed(NeedsYouRows.Profile(), NeedsYouRows.Park())).Tray;
        var colour = For(Feed(NeedsYouRows.Failure())).Tray;

        Assert.Equal((TrayState.Attention, 2), (pulse.State, pulse.Count));
        Assert.Equal((TrayState.Attention, 1), (colour.State, colour.Count));
    }

    [Fact]
    public void A_seen_row_still_counts_the_tray_follows_the_rows_urgency()
        => Assert.Equal(TrayState.Attention, For(Feed(NeedsYouRows.Park())).Tray.State);   // seen is presentation state of the list; the summary never takes it

    [Fact]
    public void Tray_is_calm_with_a_small_count_when_only_calm_rows_exist()
    {
        var tray = For(Feed(NeedsYouRows.Profile())).Tray;

        Assert.Equal((TrayState.Calm, 1), (tray.State, tray.Count));
    }

    [Fact]
    public void Tray_is_neutral_when_nothing_needs_you()
    {
        var tray = For(Feed()).Tray;

        Assert.Equal((TrayState.Neutral, 0), (tray.State, tray.Count));
    }

    [Fact]
    public void Tray_tooltips_match_spec_5_word_for_word()
    {
        Assert.Equal("Sindri · 3 running · nothing needs you", For(Feed()).Tray.Tooltip);
        Assert.Equal("Sindri · 2 need you · 3 running", For(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure())).Tray.Tooltip);
        Assert.Equal("Sindri · 1 to look at", For(Feed(NeedsYouRows.Profile())).Tray.Tooltip);
        Assert.Equal("Sindri not responding", For(Feed(NeedsYouRows.Park()), ShellState.Stale).Tray.Tooltip);
        Assert.Equal("Sindri isn't running · run sindri", For(Feed(NeedsYouRows.Park()), ShellState.NotRunning).Tray.Tooltip);
    }

    [Fact]
    public void Tray_menu_lists_the_first_five_rows_kind_id_title_age()
    {
        var rows = new[] { NeedsYouRows.Park(minutes: 12), NeedsYouRows.Decision(), NeedsYouRows.Failure(), NeedsYouRows.Profile(), NeedsYouRows.Many(1), NeedsYouRows.Many(2) };

        var tray = For(Feed(rows)).Tray;

        Assert.Equal(["park:ITM-01JC8", "decision:DEC-1", "failure:service:index", "profile:drift", "park:ITM-00001"], [.. tray.MenuRows.Select(r => r.Id)]);
        Assert.Equal("PARKED ITM-01JC8 Retry storm in outbox dispatcher · 12m", tray.Rows[0].Label);
        Assert.Equal("FAILED Nightly index build · 9h", tray.Rows[2].Label);
        Assert.Equal("Running: 3", tray.RunningLabel);
    }

    [Fact]
    public void Tray_stale_and_not_running_win_over_counts()
    {
        var feed = Feed(NeedsYouRows.Park(), NeedsYouRows.Failure());

        var stale = For(feed, ShellState.Stale).Tray;
        var down = For(feed, ShellState.NotRunning).Tray;

        Assert.Equal((TrayState.Stale, 0, 0), (stale.State, stale.Count, stale.Rows.Count));
        Assert.Equal((TrayState.NotRunning, 0, 0), (down.State, down.Count, down.Rows.Count));
        Assert.Equal(0, For(feed, ShellState.NotRunning).Badge);
        Assert.Equal(2, For(feed, ShellState.Stale).Badge);   // the rows are kept, marked stale
    }

    [Fact]
    public void Summary_line_reads_N_need_you_N_running_N_with_you_N_queued()
    {
        Assert.Equal("2 need you · 3 running · 1 with you · 4 queued", For(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure())).SummaryLine);
        Assert.Equal("", For(feed: null).SummaryLine);
    }

    [Fact]
    public void Announces_N_now_need_you_once_per_rise()
    {
        var announcer = new AttentionAnnouncer();

        announcer.Observe(0);
        Assert.Equal((0, ""), (announcer.Announcements, announcer.Line));
        announcer.Observe(2);
        announcer.Observe(2);
        Assert.Equal((1, "2 now need you"), (announcer.Announcements, announcer.Line));
        announcer.Observe(3);
        Assert.Equal((2, "3 now need you"), (announcer.Announcements, announcer.Line));
        announcer.Observe(1);   // rows leaving are announced by the list ("<id> cleared")
        Assert.Equal(2, announcer.Announcements);
        announcer.Observe(2);
        Assert.Equal((3, "2 now need you"), (announcer.Announcements, announcer.Line));
    }

    [Fact]
    public void Tooltip_adds_agents_and_asking_when_sessions_are_served()
    {
        var tray = AttentionSummary.For(Feed(NeedsYouRows.Park()), ShellState.Normal, A.Noon, new SessionCountsView(Live: 3, Asking: 1, Problems: 0)).Tray;

        Assert.EndsWith(Labels.AgentsCounts(3, 1), tray.Tooltip, StringComparison.Ordinal);
        Assert.StartsWith(For(Feed(NeedsYouRows.Park())).Tray.Tooltip, tray.Tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void Tooltip_is_unchanged_without_sessions()
    {
        var feed = Feed(NeedsYouRows.Park());

        var without = AttentionSummary.For(feed, ShellState.Normal, A.Noon, sessions: null).Tray;
        var plain = For(feed).Tray;

        Assert.Equal((plain.State, plain.Tooltip), (without.State, without.Tooltip));
        Assert.DoesNotContain("agent", without.Tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void Tooltip_stays_the_state_word_when_the_tray_is_not_normal()
    {
        var tray = AttentionSummary.For(feed: null, ShellState.NotRunning, A.Noon, new SessionCountsView(Live: 3, Asking: 1, Problems: 0)).Tray;

        Assert.Equal(Labels.TrayNotRunning, tray.Tooltip);
    }

    [Fact]
    public void Badge_stays_the_needs_you_count()
    {
        var summary = AttentionSummary.For(Feed(NeedsYouRows.Park()), ShellState.Normal, A.Noon, new SessionCountsView(Live: 5, Asking: 2, Problems: 0));

        Assert.Equal(1, summary.Badge);
        Assert.Equal("(1) Needs you · Sindri", summary.Title);
    }
}
