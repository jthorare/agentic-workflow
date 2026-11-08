using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The 'todo' view's fold (todo spec §4.2): a pure row per todo from the nine todo events, the same after a rebuild.</summary>
public sealed class TodosProjectionTests
{
    private static readonly TodoId _id = TodoHarness.First;
    private readonly TodoHarness _h = new();

    // rows hold lists, which records compare by reference: compare what they write
    private static string Json<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    private static string[] Links => ["sindri://linear/ENG-12"];

    [Fact, Trait("Lane", "unit")]
    public async Task Added_makes_a_version_1_row_keeping_who_added_it()
    {
        await _h.Seed(_id, "rotate the staging key", by: "human:cli", due: new DateOnly(2026, 11, 10), via: "pipe");

        var row = (await _h.Row(_id))!;

        Assert.Equal((_id, "rotate the staging key", new DateOnly(2026, 11, 10), 1L), (row.Id, row.Text, row.Due, row.Version));
        Assert.Equal(("human:cli", "pipe", "human:cli", "pipe"), (row.AddedBy, row.AddedVia, row.LastBy, row.LastVia));
        Assert.Equal((_h.Slice.Clock.GetUtcNow(), null, false), (row.AddedAt, row.DoneAt, row.Removed));
        Assert.Null(row.Conflict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changed_sets_the_asked_fields_moves_the_version_and_the_last_writer_but_not_the_adder()
    {
        await _h.Seed(_id, "reply to Sam", by: "human:ui", due: new DateOnly(2026, 11, 13));
        await _h.Fold(EventTypes.TodoChanged, new TodoChanged(_id, Version: 2, Text: "reply to Sam about the offsite", Due: null, ClearDue: false, Links: Links, By: "session:mcp"));

        var row = (await _h.Row(_id))!;

        Assert.Equal(("reply to Sam about the offsite", new DateOnly(2026, 11, 13), 2L), (row.Text, row.Due, row.Version));
        Assert.Equal(Links, row.Links);
        Assert.Equal(("human:ui", "session:mcp"), (row.AddedBy, row.LastBy));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changed_with_ClearDue_drops_the_date_and_with_an_empty_list_drops_the_links()
    {
        await _h.Seed(_id, due: new DateOnly(2026, 11, 13));
        await _h.Fold(EventTypes.TodoChanged, new TodoChanged(_id, Version: 2, Text: null, Due: null, ClearDue: false, Links: Links, By: "human:ui"));
        await _h.Fold(EventTypes.TodoChanged, new TodoChanged(_id, Version: 3, Text: null, Due: null, ClearDue: true, Links: [], By: "human:ui"));

        var row = (await _h.Row(_id))!;

        Assert.Null(row.Due);
        Assert.Empty(row.Links);
        Assert.Equal(3L, row.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Done_sets_doneAt_to_the_event_time_and_reopened_clears_it()
    {
        await _h.Seed(_id);
        _h.Slice.Clock.Advance(TimeSpan.FromMinutes(5));
        await _h.Fold(EventTypes.TodoDone, new TodoDone(_id, Version: 2, By: "human:ui"));
        var done = (await _h.Row(_id))!;
        await _h.Fold(EventTypes.TodoReopened, new TodoReopened(_id, Version: 3, By: "human:ui"));
        var reopened = (await _h.Row(_id))!;

        Assert.Equal((_h.Slice.Clock.GetUtcNow(), 2L), (done.DoneAt, done.Version));
        Assert.Equal((null, 3L), (reopened.DoneAt, reopened.Version));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Removed_marks_the_row_with_when_and_by_and_restored_brings_it_back()
    {
        await _h.Seed(_id);
        _h.Slice.Clock.Advance(TimeSpan.FromMinutes(5));
        await _h.Fold(EventTypes.TodoRemoved, new TodoRemoved(_id, Version: 2, By: "session:mcp"));
        var removed = (await _h.Row(_id))!;
        await _h.Fold(EventTypes.TodoRestored, new TodoRestored(_id, Version: 3, By: "human:ui"));
        var restored = (await _h.Row(_id))!;

        Assert.Equal((true, _h.Slice.Clock.GetUtcNow(), "session:mcp"), (removed.Removed, removed.RemovedAt, removed.RemovedBy));
        Assert.Equal((false, null, null, 3L), (restored.Removed, restored.RemovedAt, restored.RemovedBy, restored.Version));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_conflict_opens_with_both_sides_a_pick_closes_it_and_undoing_the_pick_opens_it_again_with_the_stored_sides()
    {
        var sindri = new TodoSide("reply to Sam", Due: null, Links: [], Done: true, Removed: false);
        var obsidian = new TodoSide("reply to Sam about the offsite", Due: null, Links: [], Done: false, Removed: false);
        await _h.Seed(_id);
        await _h.Fold(EventTypes.TodoConflicted, new TodoConflicted(_id, sindri, obsidian, NoteHash: "ab12"));
        var open = (await _h.Row(_id))!.Conflict;
        await _h.Fold(EventTypes.TodoConflictResolved, new TodoConflictResolved(_id, TodoSideName.Obsidian, BaseLine: "- [ ] reply", By: "human:ui"));
        var picked = (await _h.Row(_id))!.Conflict;
        await _h.Fold(EventTypes.TodoConflictReopened, new TodoConflictReopened(_id, By: "human:ui"));
        var again = (await _h.Row(_id))!.Conflict;

        Assert.Equal((Json(sindri), Json(obsidian), "ab12"), (Json(open!.Sindri), Json(open.Obsidian), open.NoteHash));
        Assert.Null(picked);
        Assert.Equal(Json(open), Json(again));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_event_for_a_todo_the_view_does_not_hold_is_skipped()
    {
        await _h.Fold(EventTypes.TodoDone, new TodoDone(_id, Version: 2, By: "human:ui"));

        Assert.Null(await _h.Row(_id));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_from_the_same_events_gives_the_same_rows()
    {
        LedgerEvent[] ledger =
        [
            Recorded.Of(_h.Slice.Events, EventTypes.TodoAdded, new TodoAdded(_id, "a", Due: new DateOnly(2026, 11, 13), Links: Links, ClientId: TodoClientId.From("c-1"), By: "human:cli"), sequence: 1),
            Recorded.Of(_h.Slice.Events, EventTypes.TodoAdded, new TodoAdded(TodoHarness.Second, "b", Due: null, Links: [], ClientId: null, By: "session:mcp"), sequence: 2),
            Recorded.Of(_h.Slice.Events, EventTypes.TodoDone, new TodoDone(_id, Version: 2, By: "human:ui"), sequence: 3),
            Recorded.Of(_h.Slice.Events, EventTypes.TodoRemoved, new TodoRemoved(TodoHarness.Second, Version: 2, By: "human:ui"), sequence: 4),
        ];
        foreach (var recorded in ledger)
        {
            await _h.Views.Apply([recorded]);
        }

        var rebuilt = new TodoHarness();
        await rebuilt.Views.Apply(ledger);

        Assert.NotNull(await rebuilt.Row(_id));
        Assert.Equal(Json(await _h.Row(_id)), Json(await rebuilt.Row(_id)));
        Assert.Equal(Json(await _h.Row(TodoHarness.Second)), Json(await rebuilt.Row(TodoHarness.Second)));
    }
}
