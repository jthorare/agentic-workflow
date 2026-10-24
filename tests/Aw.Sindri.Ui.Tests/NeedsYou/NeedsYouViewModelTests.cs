using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The Needs-you list view model (U3' T3): rows as served, the cap, selection, the hold and the banners.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouViewModelTests
{
    private const string Empty = "Nothing needs you. 3 running, 4 queued.";

    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeMotionPreference _os = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<NeedsYouViewModel> List(params NeedsYouEntry[] rows)
    {
        var list = new NeedsYouViewModel(new Preferences(_store), _os, _clock);
        await list.Restore(Token);
        list.Update(Feed(rows));
        return list;
    }

    private static NeedsYouFeed Feed(params NeedsYouEntry[] rows) => new(rows, rows.Length == 0 ? Empty : "");

    private static string[] Ids(NeedsYouViewModel list) => [.. list.Rows.Select(r => r.Id)];

    [Fact]
    public async Task Rows_keep_the_apis_order()
    {
        var list = await List(NeedsYouRows.Profile(), NeedsYouRows.Park(), NeedsYouRows.Failure());

        Assert.Equal(["profile:drift", "park:ITM-01JC8", "failure:service:index"], Ids(list));
    }

    [Fact]
    public async Task Row_shows_chip_id_title_repo_reason_and_age()
    {
        var list = await List(NeedsYouRows.Park(minutes: 12), NeedsYouRows.Profile(), NeedsYouRows.Failure());

        var park = list.Rows[0];
        Assert.Equal(("PARKED", ChipTone.Attn, "ITM-01JC8", "Retry storm in outbox dispatcher", "agentic-workflow"),
            (park.Chip!.Word, park.Chip!.Tone, park.ShownId, park.Title, park.Repo));
        Assert.Equal(("checks failed twice with the same findings", "12m"), (park.Label, park.Age));
        Assert.Equal(["12m", "2h", "9h"], [.. list.Rows.Select(r => r.Age)]);
    }

    [Fact]
    public async Task Age_follows_the_clock_on_the_next_refresh()
    {
        var list = await List(NeedsYouRows.Park(minutes: 12));

        _clock.Advance(TimeSpan.FromMinutes(3));
        list.Update(Feed(NeedsYouRows.Park(minutes: 12)));

        Assert.Equal("15m", list.Rows[0].Age);
    }

    [Fact]
    public async Task Row_is_named_for_screen_readers_by_urgency_chip_title_and_id()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Failure());

        Assert.Equal(["needs you now, PARKED, Retry storm in outbox dispatcher, ITM-01JC8, new", "failed, FAILED, Nightly index build, failure:service:index"], [.. list.Rows.Select(r => r.AccessibleName)]);
    }

    [Fact]
    public async Task Primary_control_first_then_the_rows_other_actions()
    {
        var shuffled = NeedsYouRows.Failure() with
        {
            Actions = [.. NeedsYouRows.Failure().Actions.Reverse()],
        };
        var list = await List(shuffled);

        var controls = list.Rows[0].Controls;

        Assert.Equal(["RunService", "PauseService"], [.. controls.Select(c => c.Action.CatalogId)]);
        Assert.Equal([true, false], controls.Select(c => c.IsPrimary));
    }

    [Fact]
    public async Task Actions_render_ready_or_pending_from_the_catalog()
    {
        var list = await List(NeedsYouRows.Decision(), NeedsYouRows.Failure(), NeedsYouRows.Park());

        Assert.Equal([true, true], list.Rows[0].Controls.Select(c => c.IsEnabled));
        Assert.Equal([false, false], list.Rows[1].Controls.Select(c => c.IsEnabled));
        Assert.Equal([true], list.Rows[2].Controls.Select(c => c.IsEnabled));
    }

    [Fact]
    public async Task Failure_row_shows_RunService_and_PauseService_pending_with_the_served_cli()
    {
        var list = await List(NeedsYouRows.Failure());

        var controls = list.Rows[0].Controls;

        Assert.Equal(["sindri services run index", "sindri services pause index"], [.. controls.Select(c => c.Action.Cli)]);
        Assert.All(controls, c => Assert.Equal(ActionReadiness.Pending, c.Action.Readiness));
        Assert.Equal("sindri services run index", controls[0].Tooltip);
    }

    [Fact]
    public async Task Profile_row_exposes_a_ready_approve_with_the_served_cli()
    {
        var list = await List(NeedsYouRows.Profile(new NeedsYouSubject("profile", "3f1c09")));

        var approve = Assert.Single(list.Rows[0].Controls);

        Assert.True(approve.IsEnabled);
        Assert.Equal("sindri profile approve 3f1c09", approve.Action.Cli);
        Assert.Empty(approve.Tooltip);
        Assert.Equal("sindri profile approve 3f1c09", list.Rows[0].Cli);
    }

    [Fact]
    public async Task Profile_row_with_a_null_subject_keys_on_its_id_and_opens_its_sheet()
    {
        var list = await List(NeedsYouRows.Profile(subject: null));

        Assert.Equal("profile:drift", list.Rows[0].Id);
        Assert.Equal(new DeepLink(DeepLinkKind.NeedsYou, "profile:drift"), list.Rows[0].SheetLink);
    }

    [Fact]
    public async Task Decision_row_offers_Open_only_when_item_is_served()
    {
        var list = await List(NeedsYouRows.Decision(item: "ITM-01JD2"), NeedsYouRows.Decision("decision:DEC-2", item: null));

        Assert.Equal([true, false], list.Rows.Select(r => r.CanOpenItem));
        Assert.Equal(new DeepLink(DeepLinkKind.Item, "ITM-01JD2"), list.Rows[0].ItemLink);
        Assert.Null(list.Rows[1].ItemLink);
    }

    [Fact]
    public async Task Home_caps_at_five_with_the_overflow_reading_all_N()
    {
        var list = await List([.. Enumerable.Range(1, 7).Select(NeedsYouRows.Many)]);

        Assert.Equal(5, list.Rows.Count);
        Assert.Equal(["park:ITM-00001", "park:ITM-00002", "park:ITM-00003", "park:ITM-00004", "park:ITM-00005"], Ids(list));
        Assert.Equal((7, "all 7 →", true), (list.Count, list.OverflowText, list.HasOverflow));
    }

    [Fact]
    public async Task A_list_within_the_cap_has_no_overflow()
    {
        var list = await List([.. Enumerable.Range(1, 5).Select(NeedsYouRows.Many)]);

        Assert.Equal((5, "", false), (list.Rows.Count, list.OverflowText, list.HasOverflow));
    }

    [Fact]
    public async Task Show_all_expands_in_place()
    {
        var list = await List([.. Enumerable.Range(1, 7).Select(NeedsYouRows.Many)]);

        list.ShowAll();

        Assert.Equal(7, list.Rows.Count);
        Assert.False(list.HasOverflow);
        Assert.True(list.IsExpanded);
    }

    [Fact]
    public async Task Text_scale_1_3_drops_the_cap_to_four()
    {
        var list = await List([.. Enumerable.Range(1, 7).Select(NeedsYouRows.Many)]);

        list.TextScale = 1.3;

        Assert.Equal(4, list.Rows.Count);
        Assert.Equal("all 7 →", list.OverflowText);
    }

    [Fact]
    public async Task Empty_shows_the_served_sentence()
    {
        var list = await List();

        Assert.Equal((true, Empty, false), (list.IsEmpty, list.EmptyText, list.HasBanner));
        Assert.Empty(list.Rows);
    }

    [Fact]
    public async Task Stalled_keeps_rows_marked_stale_with_the_banner()
    {
        var list = await List();
        const string Sentence = "Views are stalled at 40: run `sindri views rebuild`";

        list.Update(new NeedsYouFeed([NeedsYouRows.Park()], Sentence) { Stalled = true });

        Assert.Equal((true, Sentence, false), (list.HasBanner, list.Banner, list.IsEmpty));
        Assert.True(list.Rows[0].IsStale);
    }

    [Fact]
    public async Task Rebuilding_disables_actions_that_read_the_views_and_says_to_retry_after_the_rebuild()
    {
        var list = await List();

        list.Update(new NeedsYouFeed([NeedsYouRows.Decision()], "Rebuilding views (cursor 4 of 9)…") { Rebuilding = true });

        Assert.Equal("Rebuilding views (cursor 4 of 9)…", list.Banner);
        Assert.All(list.Rows[0].Controls, c => Assert.Equal((false, Labels.RetryAfterRebuild), (c.IsEnabled, c.Tooltip)));

        list.Update(Feed(NeedsYouRows.Decision()));

        Assert.All(list.Rows[0].Controls, c => Assert.True(c.IsEnabled));
        Assert.False(list.HasBanner);
    }

    [Fact]
    public async Task Recorded_only_row_shows_the_served_notice()
    {
        var recorded = NeedsYouRows.Decision() with { RecordedOnly = true, Notice = "Recorded now; the engine acts on it when the workflow runs." };
        var list = await List(recorded, NeedsYouRows.Park());

        Assert.Equal((true, "Recorded now; the engine acts on it when the workflow runs."), (list.Rows[0].HasNotice, list.Rows[0].Notice));
        Assert.Equal((false, ""), (list.Rows[1].HasNotice, list.Rows[1].Notice));
    }

    [Fact]
    public async Task Selection_is_kept_by_row_id_across_refreshes()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Decision(), NeedsYouRows.Failure());
        list.Select("decision:DEC-1");

        list.Update(Feed(NeedsYouRows.Profile(), NeedsYouRows.Park(), NeedsYouRows.Decision(), NeedsYouRows.Failure()));

        Assert.Equal("decision:DEC-1", list.SelectedId);
        Assert.True(list.Rows[2].IsSelected);
        Assert.False(list.Rows[1].IsSelected);
    }

    [Fact]
    public async Task A_cleared_selected_row_moves_to_the_next_and_announces_id_cleared()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Decision(), NeedsYouRows.Failure());
        list.Select("decision:DEC-1");

        list.Update(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));

        Assert.Equal(("failure:service:index", "REQ-01JD4 cleared"), (list.SelectedId, list.Announcement));
    }

    [Fact]
    public async Task A_cleared_last_row_moves_selection_to_the_one_before()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Failure());
        list.Select("failure:service:index");

        list.Update(Feed(NeedsYouRows.Park()));

        Assert.Equal("park:ITM-01JC8", list.SelectedId);
    }

    [Fact]
    public async Task Clearing_every_row_leaves_nothing_selected()
    {
        var list = await List(NeedsYouRows.Park());
        list.Select("park:ITM-01JC8");

        list.Update(Feed());

        Assert.Null(list.SelectedId);
        Assert.Equal("ITM-01JC8 cleared", list.Announcement);
    }

    [Fact]
    public async Task Pointer_over_the_panel_holds_new_rows_behind_N_new()
    {
        var list = await List(NeedsYouRows.Park());
        list.PointerOver = true;

        list.Update(Feed(NeedsYouRows.Decision(), NeedsYouRows.Park(), NeedsYouRows.Failure()));

        Assert.Equal(["park:ITM-01JC8"], Ids(list));
        Assert.Equal((2, "2 new", true), (list.HeldCount, list.HeldText, list.HasHeld));

        list.PointerOver = false;

        Assert.Equal(["decision:DEC-1", "park:ITM-01JC8", "failure:service:index"], Ids(list));
        Assert.Equal((0, false), (list.HeldCount, list.HasHeld));
    }

    [Fact]
    public async Task N_new_shows_the_held_rows_while_the_pointer_stays()
    {
        var list = await List(NeedsYouRows.Park());
        list.PointerOver = true;
        list.Update(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));

        list.ShowHeld();

        Assert.Equal(["park:ITM-01JC8", "failure:service:index"], Ids(list));
    }

    [Fact]
    public async Task A_row_that_leaves_is_removed_even_while_the_pointer_holds_new_ones()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Failure());
        list.PointerOver = true;

        list.Update(Feed(NeedsYouRows.Failure(), NeedsYouRows.Decision()));

        Assert.Equal(["failure:service:index"], Ids(list));
        Assert.Equal(1, list.HeldCount);
    }
}
