using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The pinned card's footer, one test per row of the §3.7 table (concept pins spec, phase Q7b).</summary>
public sealed class PinCardLinesTests
{
    private static PinView Pin(string state = "approved", string? change = "created") =>
        new("billing", "Billing", Summary: null, [], [], FolderRules: 0, "profile", state, change);

    private static MapRebuildView Rebuild(string phase = "idle", string? progress = null, DateTimeOffset? retryAt = null, bool stale = false, string? failed = null) =>
        new(phase, progress, retryAt, stale, failed);

    private static PinCardLines Of(PinView pin, MapRebuildView rebuild, string? problem = null, string? moved = null) =>
        PinLineRules.Of(pin, rebuild, problem, moved, TimeZoneInfo.Utc);

    [Fact, Trait("Lane", "unit")]
    public void A_pin_written_but_not_approved_says_other_changes_wait_and_offers_Review_and_approve()
    {
        var lines = Of(Pin("waiting", change: null), Rebuild());

        Assert.Equal(["Pinned, not approved yet", "Other changes are waiting, so this needs your review."], lines.Lines);
        Assert.Equal(PinCardAction.ReviewAndApprove, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_removal_not_approved_says_unpinned_and_offers_Review_and_approve()
    {
        var lines = Of(Pin("removing", change: "removed"), Rebuild());

        Assert.Equal(["Unpinned, not approved yet", "Other changes are waiting, so this needs your review."], lines.Lines);
        Assert.Equal(PinCardAction.ReviewAndApprove, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_held_map_blocks_the_rebuild_and_offers_Open_review()
    {
        var lines = Of(Pin(), Rebuild("mapHeld", stale: true));

        Assert.Equal(["Pinned · the rebuild waits until you've looked at the held map"], lines.Lines);
        Assert.Equal(PinCardAction.OpenReview, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_repo_on_its_main_branch_says_so_without_a_button()
    {
        var lines = Of(Pin(), Rebuild("noMainBranch", stale: true));

        Assert.Equal(["Pinned · the rebuild waits until a repo is back on its main branch"], lines.Lines);
        Assert.Equal(PinCardAction.None, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_backoff_names_the_next_try_in_local_time_and_offers_Try_again()
    {
        var lines = Of(Pin(), Rebuild("backoff", retryAt: new DateTimeOffset(2026, 10, 31, 14, 5, 40, TimeSpan.Zero), stale: true, failed: "SND-CONTEXT-040"));

        Assert.Equal(["Pinned · the last refreshes failed; the next try is at 14:05"], lines.Lines);
        Assert.Equal(PinCardAction.TryAgain, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_idle_stale_map_with_a_change_says_the_rebuild_starts_within_a_minute()
    {
        var lines = Of(Pin(), Rebuild(stale: true));

        Assert.Equal(["Pinned · the map rebuild starts within a minute"], lines.Lines);
        Assert.Equal(PinCardAction.None, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_running_refresh_shows_its_latest_progress_line()
    {
        var lines = Of(Pin(), Rebuild("building", progress: "resolving calls", stale: true));

        Assert.Equal(["Pinned · rebuilding the map: resolving calls"], lines.Lines);
        Assert.Equal(PinCardAction.None, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_refresh_names_the_code_and_offers_Try_again()
    {
        var lines = Of(Pin(), Rebuild(stale: true, failed: "SND-CONTEXT-040"));

        Assert.Equal(["Pinned · Couldn't rebuild the map with this pin (SND-CONTEXT-040). Try again."], lines.Lines);
        Assert.Equal(PinCardAction.TryAgain, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_prefix_follows_the_change_edited_and_unpinned()
    {
        Assert.Equal(["Edited · the map rebuild starts within a minute"], Of(Pin(change: "updated"), Rebuild(stale: true)).Lines);
        Assert.Equal(["Unpinned · the map rebuild starts within a minute"], Of(Pin(change: "removed"), Rebuild(stale: true)).Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_change_the_problem_and_moved_lines_stand_alone()
    {
        var lines = Of(Pin(change: null), Rebuild(), problem: "2 of its features are gone from the map", moved: "+4 features, 3 more records linked here");

        Assert.Equal(["2 of its features are gone from the map", "+4 features, 3 more records linked here"], lines.Lines);
        Assert.Equal(PinCardAction.None, lines.Action);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_the_map_already_has_shows_no_rebuild_line_even_while_another_refresh_runs()
    {
        var lines = Of(Pin(change: null), Rebuild("building", progress: "cleaning up"));

        Assert.Empty(lines.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_footer_has_at_most_three_lines_state_then_problem_then_moved()
    {
        var lines = Of(Pin(), Rebuild(stale: true), problem: "P", moved: "M");

        Assert.Equal(["Pinned · the map rebuild starts within a minute", "P", "M"], lines.Lines);
    }
}
