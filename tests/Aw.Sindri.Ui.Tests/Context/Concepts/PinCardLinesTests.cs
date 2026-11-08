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

    private static PinMovedView Moved(int gained = 0, int lost = 0, int? records = 0, bool failed = false, params PinMovedTo[] to) =>
        new("v0", "v1", gained, lost, to, records, failed);

    private static readonly PinMovedTo _signIn = new(Id: "auth", Name: "Sign in", Features: 2);
    private static readonly PinMovedTo _nowhere = new(Id: null, Name: null, Features: 1);

    [Fact, Trait("Lane", "unit")]
    public void A_pin_that_gained_features_and_records_shows_what_moved()
    {
        Assert.Equal("+4 features, 3 more records linked here", PinMovedRules.Line(Moved(gained: 4, records: 3)));
        Assert.Equal("+1 feature, 1 more record linked here", PinMovedRules.Line(Moved(gained: 1, records: 1)));
    }

    [Fact, Trait("Lane", "unit")]
    public void While_the_relink_runs_the_line_says_records_are_linking() =>
        Assert.Equal("+4 features; records linking…", PinMovedRules.Line(Moved(gained: 4, records: null)));

    [Fact, Trait("Lane", "unit")]
    public void A_pin_that_lost_features_names_where_they_went() =>
        Assert.Equal("\u22122 features (now in Sign in)", PinMovedRules.Line(Moved(lost: 2, to: _signIn)));

    [Fact, Trait("Lane", "unit")]
    public void Lost_features_that_split_between_two_places_give_a_count_for_each()
    {
        Assert.Equal("\u22123 features (2 now in Sign in, 1 in no concept)", PinMovedRules.Line(Moved(lost: 3, to: [_signIn, _nowhere])));
        Assert.Equal("\u22125 features (3 now in Notify, 2 in Sign in)", PinMovedRules.Line(Moved(lost: 5, to: [new(Id: "a", Name: "Notify", Features: 3), _signIn])));
    }

    [Fact, Trait("Lane", "unit")]
    public void Lost_features_over_more_than_two_places_name_two_and_count_the_rest()
    {
        var notify = new PinMovedTo(Id: "a", Name: "Notify", Features: 3);
        var search = new PinMovedTo(Id: "c", Name: "Search", Features: 1);

        Assert.Equal("\u22126 features (now in Notify, Sign in and 1 other)", PinMovedRules.Line(Moved(lost: 6, to: [notify, _signIn, search])));
        Assert.Equal("\u22127 features (now in Notify, Sign in and 2 others)", PinMovedRules.Line(Moved(lost: 7, to: [notify, _signIn, search, new(Id: "d", Name: "Billing", Features: 1)])));
    }

    [Fact, Trait("Lane", "unit")]
    public void Features_that_left_into_no_concept_say_so()
    {
        Assert.Equal("\u22121 feature (now in no concept)", PinMovedRules.Line(Moved(lost: 1, to: _nowhere)));
        Assert.Equal("\u22121 feature (now in no concept)", PinMovedRules.Line(Moved(lost: 1, to: [])));
        Assert.Equal("\u22121 feature (now in no concept)", PinMovedRules.Line(Moved(lost: 1) with { To = null! }));
    }

    [Fact, Trait("Lane", "unit")]
    public void Fewer_records_linked_reads_as_fewer() =>
        Assert.Equal("+2 features, \u22123 features (2 now in Sign in, 1 in no concept), 5 fewer records linked here", PinMovedRules.Line(Moved(gained: 2, lost: 3, records: -5, to: [_signIn, _nowhere])));

    [Fact, Trait("Lane", "unit")]
    public void Records_alone_and_singular_records_read_plainly()
    {
        Assert.Equal("3 more records linked here", PinMovedRules.Line(Moved(records: 3)));
        Assert.Equal("1 fewer record linked here", PinMovedRules.Line(Moved(records: -1)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_relink_says_records_are_not_linked_yet() =>
        Assert.Equal("+2 features; records not linked yet, retrying", PinMovedRules.Line(Moved(gained: 2, records: null, failed: true)));

    [Fact, Trait("Lane", "unit")]
    public void A_failed_relink_with_no_feature_moved_shows_the_failure_alone() =>
        Assert.Equal("Records not linked yet, retrying", PinMovedRules.Line(Moved(records: null, failed: true)));

    [Fact, Trait("Lane", "unit")]
    public void A_pin_that_did_not_move_has_no_line()
    {
        Assert.Null(PinMovedRules.Line(moved: null));
        Assert.Null(PinMovedRules.Line(Moved(records: null)));
        Assert.Null(PinMovedRules.Line(Moved(records: 0)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_problem_line_comes_before_what_moved()
    {
        var lines = Of(Pin(change: null), Rebuild(), problem: "2 of its features are gone from the map", moved: "+4 features, 3 more records linked here");

        Assert.Equal(["2 of its features are gone from the map", "+4 features, 3 more records linked here"], lines.Lines);
    }
}
