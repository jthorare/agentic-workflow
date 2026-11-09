using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The Needs-you list's keys (U3' T3): they act on the selected row.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouKeysTests
{
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeMotionPreference _os = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly List<NeedsYouActionEventArgs> _actions = [];
    private readonly List<NeedsYouCopyEventArgs> _copies = [];
    private readonly List<string> _sheets = [];
    private readonly List<string> _focus = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<NeedsYouViewModel> List(params NeedsYouEntry[] rows)
    {
        var list = new NeedsYouViewModel(new Preferences(_store), _os, _clock);
        await list.Restore(Token);
        list.ActionRequested += (_, e) => _actions.Add(e);
        list.CopyRequested += (_, e) => _copies.Add(e);
        list.SheetRequested += (_, e) => _sheets.Add(e.RowId);
        list.FocusRequested += (_, e) => _focus.Add(e.RowId);
        list.Update(new NeedsYouFeed(rows, ""));
        return list;
    }

    private static void Tap(NeedsYouViewModel list, NeedsYouKey key) => _ = list.Press(key);

    // A pulse row whose primary the app may run (a ready action).
    private static NeedsYouEntry Runnable() => NeedsYouRows.Park("park:ready") with
    {
        PrimaryAction = "Resume",
        Actions = [new NeedsYouAction("Resume", "Resume", ActionReadiness.Ready, "sindri resume ITM-01JC8", RejectLike: false)],
    };

    [Fact]
    public async Task J_and_K_move_the_selection_and_stop_at_the_ends()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Decision(), NeedsYouRows.Failure());

        Assert.True(list.Press(NeedsYouKey.J));
        Assert.Equal("park:ITM-01JC8", list.SelectedId);
        Tap(list, NeedsYouKey.J);
        Tap(list, NeedsYouKey.J);
        Tap(list, NeedsYouKey.J);
        Assert.Equal("failure:service:index", list.SelectedId);
        Tap(list, NeedsYouKey.K);
        Assert.Equal("decision:DEC-1", list.SelectedId);
        Tap(list, NeedsYouKey.K);
        Tap(list, NeedsYouKey.K);
        Assert.Equal("park:ITM-01JC8", list.SelectedId);
    }

    [Fact]
    public async Task J_moves_only_within_the_rows_shown()
    {
        var list = await List([.. Enumerable.Range(1, 7).Select(NeedsYouRows.Many)]);

        for (var i = 0; i < 9; i++)
        {
            Tap(list, NeedsYouKey.J);
        }

        Assert.Equal("park:ITM-00005", list.SelectedId);
    }

    [Fact]
    public async Task A_runs_the_single_ready_primary_and_not_a_pending_one()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Failure(), Runnable());

        list.Select("failure:service:index");
        Assert.False(list.Press(NeedsYouKey.A));   // pending
        Assert.Empty(_actions);

        list.Select("park:ready");
        Assert.True(list.Press(NeedsYouKey.A));

        var run = Assert.Single(_actions);
        Assert.Equal(("park:ready", "Resume"), (run.RowId, run.Action.CatalogId));
    }

    [Fact]
    public async Task Acting_on_a_row_marks_it_seen()
    {
        var list = await List(Runnable());
        list.Select("park:ready");
        Assert.True(list.Rows[0].IsPulsing);

        Tap(list, NeedsYouKey.A);

        Assert.False(list.Rows[0].IsPulsing);
    }

    [Fact]
    public async Task A_does_nothing_while_the_views_rebuild()
    {
        var list = await List();
        list.Update(new NeedsYouFeed([Runnable()], "Rebuilding views…") { Rebuilding = true });
        list.Select("park:ready");

        Assert.False(list.Press(NeedsYouKey.A));
        Assert.Empty(_actions);
    }

    // Review run 3 (corr-2): a decision served without its options has no option to send, so A does nothing and marks nothing seen.
    [Fact]
    public async Task A_on_a_decision_row_without_an_option_does_nothing()
    {
        var served = NeedsYouRows.Decision();
        var list = await List(served with { Actions = [new NeedsYouAction("Decide", "Decide", ActionReadiness.Ready, "sindri decide DEC-1 <choice>", RejectLike: false)], PrimaryAction = "Decide" });
        list.Select("decision:DEC-1");
        Tap(list, NeedsYouKey.Enter);

        Assert.False(list.Press(NeedsYouKey.A));
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task A_on_a_decision_row_does_nothing_unless_its_sheet_is_open()
    {
        var list = await List(NeedsYouRows.Decision());
        list.Select("decision:DEC-1");

        Assert.False(list.Press(NeedsYouKey.A));
        Assert.Empty(_actions);

        Tap(list, NeedsYouKey.Enter);
        Assert.True(list.Press(NeedsYouKey.A));

        var run = Assert.Single(_actions);
        Assert.Equal("accept", run.Action.OptionId);   // the primary: the recommended option, first as served
    }

    [Fact]
    public async Task X_on_a_decision_picks_the_rejectLike_option()
    {
        var list = await List(NeedsYouRows.Decision());
        list.Select("decision:DEC-1");

        Assert.True(list.Press(NeedsYouKey.X));

        var run = Assert.Single(_actions);
        Assert.Equal(("decision:DEC-1", "decline", true), (run.RowId, run.Action.OptionId, run.Action.RejectLike));
    }

    [Fact]
    public async Task X_does_nothing_on_a_row_without_a_rejectLike_option()
    {
        var list = await List(Runnable(), NeedsYouRows.Failure());

        list.Select("park:ready");
        Assert.False(list.Press(NeedsYouKey.X));
        list.Select("failure:service:index");
        Assert.False(list.Press(NeedsYouKey.X));

        Assert.Empty(_actions);
    }

    [Fact]
    public async Task Y_copies_the_rows_cli_and_copies_nothing_when_none_is_served()
    {
        var list = await List(NeedsYouRows.Profile(), NeedsYouRows.Park() with { Cli = "" });

        list.Select("profile:drift");
        Assert.True(list.Press(NeedsYouKey.Y));
        list.Select("park:ITM-01JC8");
        Assert.False(list.Press(NeedsYouKey.Y));   // no command served

        var copy = Assert.Single(_copies);
        Assert.Equal(("profile:drift", "sindri profile approve 3f1c09"), (copy.RowId, copy.Text));
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task Enter_opens_the_sheet_and_Esc_returns_focus_to_the_row()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Decision());
        list.Select("decision:DEC-1");

        Assert.True(list.Press(NeedsYouKey.Enter));
        Assert.Equal("decision:DEC-1", list.SheetRowId);
        Assert.Equal(["decision:DEC-1"], _sheets);

        Assert.True(list.Press(NeedsYouKey.Escape));
        Assert.Null(list.SheetRowId);
        Assert.Equal(["decision:DEC-1"], _focus);
    }

    [Fact]
    public async Task Esc_with_no_sheet_open_is_not_used()
    {
        var list = await List(NeedsYouRows.Park());
        list.Select("park:ITM-01JC8");

        Assert.False(list.Press(NeedsYouKey.Escape));
        Assert.Empty(_focus);
    }

    [Fact]
    public async Task The_other_keys_do_nothing_without_a_selection()
    {
        var list = await List(Runnable());

        Assert.All((NeedsYouKey[])[NeedsYouKey.A, NeedsYouKey.X, NeedsYouKey.Y, NeedsYouKey.Enter, NeedsYouKey.Escape], k => Assert.False(list.Press(k)));
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task Keys_are_ignored_for_500_ms_after_an_automatic_move()
    {
        var list = await List(Runnable(), NeedsYouRows.Park());
        list.Select("park:ready");
        list.Update(new NeedsYouFeed([NeedsYouRows.Park()], ""));   // the selected row cleared; the selection moved by itself

        _clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.False(list.Press(NeedsYouKey.Y));
        Assert.False(list.Press(NeedsYouKey.J));

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(list.Press(NeedsYouKey.Y));
    }

    [Fact]
    public async Task A_move_the_user_made_does_not_start_the_quiet_time()
    {
        var list = await List(NeedsYouRows.Park(), NeedsYouRows.Profile());
        Tap(list, NeedsYouKey.J);

        Assert.True(list.Press(NeedsYouKey.J));
    }
}
