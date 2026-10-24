using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>Which needs-you rows are seen (UI spec §5): on screen and focused for 1 s, or acted on; kept in the preference file.</summary>
[Lane(TestLane.Unit)]
public sealed class SeenTrackerTests
{
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Preferences Prefs() => new(_store);

    private async Task<SeenTracker> Tracker()
    {
        var tracker = new SeenTracker(Prefs(), _clock);
        await tracker.Restore(Token);
        return tracker;
    }

    private async Task<IReadOnlyList<string>> Stored()
    {
        var prefs = Prefs();
        await prefs.Flush();
        return (await prefs.Load(Token)).SeenIds;
    }

    [Fact]
    public async Task A_row_is_seen_after_1_s_on_screen_and_focused()
    {
        var tracker = await Tracker();

        tracker.Watch("a", onScreenAndFocused: true);
        _clock.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(tracker.IsSeen("a"));

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(tracker.IsSeen("a"));
    }

    [Fact]
    public async Task A_row_shown_but_not_focused_stays_unseen()
    {
        var tracker = await Tracker();

        tracker.Watch("a", onScreenAndFocused: false);
        _clock.Advance(TimeSpan.FromSeconds(5));
        Assert.False(tracker.IsSeen("a"));

        // Losing focus part-way restarts the second.
        tracker.Watch("a", onScreenAndFocused: true);
        _clock.Advance(TimeSpan.FromMilliseconds(600));
        tracker.Watch("a", onScreenAndFocused: false);
        _clock.Advance(TimeSpan.FromSeconds(5));
        Assert.False(tracker.IsSeen("a"));
    }

    [Fact]
    public async Task Acting_on_a_row_marks_it_seen()
    {
        var tracker = await Tracker();

        tracker.Acted("a");

        Assert.True(tracker.IsSeen("a"));
    }

    [Fact]
    public async Task Seen_ids_persist_in_preferences()
    {
        var tracker = await Tracker();

        tracker.Acted("a");
        tracker.Acted("b");

        Assert.Equal(["a", "b"], await Stored(), StringComparer.Ordinal);
        Assert.True((await Tracker()).IsSeen("b"));
    }

    [Fact]
    public async Task Seen_ids_are_pruned_when_the_row_leaves_the_list()
    {
        var tracker = await Tracker();
        tracker.Acted("a");
        tracker.Acted("b");

        tracker.Retain(["b", "c"]);

        Assert.False(tracker.IsSeen("a"));
        Assert.Equal(["b"], await Stored(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Mark_all_seen_marks_every_current_row_and_none_that_arrive_later()
    {
        var tracker = await Tracker();

        tracker.MarkAllSeen(["a", "b"]);

        Assert.True(tracker.IsSeen("a") && tracker.IsSeen("b"));
        Assert.False(tracker.IsSeen("c"));
    }

    // The Undo window was 10 s (a provisional owner default); q93 raised it to 5 min so it does not vanish before it is read.
    [Fact]
    public async Task Mark_all_seen_offers_Undo_for_5_min()
    {
        var tracker = await Tracker();
        tracker.Acted("z");

        tracker.MarkAllSeen(["a", "b", "z"]);
        Assert.Equal(2, tracker.UndoCount); // z was seen already
        _clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, tracker.UndoCount);

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, tracker.UndoCount);
        Assert.False(tracker.Undo());
        Assert.True(tracker.IsSeen("a"));
    }

    [Fact]
    public async Task Undo_makes_the_rows_Mark_all_seen_marked_unseen_again_and_keeps_the_others()
    {
        var tracker = await Tracker();
        tracker.Acted("z");
        tracker.MarkAllSeen(["a", "b", "z"]);

        Assert.True(tracker.Undo());

        Assert.Equal((false, false, true), (tracker.IsSeen("a"), tracker.IsSeen("b"), tracker.IsSeen("z")));
        Assert.Equal(["z"], await Stored(), StringComparer.Ordinal);
        Assert.Equal(0, tracker.UndoCount);
    }

    // review run 1, a11y-12 (WCAG 2.2.1): the offer waits while the user is on its button.
    [Fact]
    public async Task The_Undo_offer_waits_while_it_is_held_and_starts_over_when_let_go()
    {
        var tracker = await Tracker();
        tracker.MarkAllSeen(["a", "b"]);
        _clock.Advance(TimeSpan.FromSeconds(9));

        tracker.HoldUndo(holding: true);
        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(2, tracker.UndoCount);

        tracker.HoldUndo(holding: false);
        _clock.Advance(SeenTracker.UndoFor - TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, tracker.UndoCount);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, tracker.UndoCount);
    }
}
