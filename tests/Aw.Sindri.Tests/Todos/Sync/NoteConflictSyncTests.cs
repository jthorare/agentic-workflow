using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>TP-10: the sync raises a conflict when both places changed a todo, keeps its hands off the todo until the owner picks, closes it when the two agree, and honours a pick.</summary>
public sealed class NoteConflictSyncTests
{
    private static readonly TodoId _first = TodoHarness.First;

    // one todo, synced; then Sindri says "today" and the note says "later"; the pass raises the conflict and the view carries it
    private static async Task<SyncRig> WithOpenConflict()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(_first, "reply to Sam");
        _ = await rig.Pass();
        await rig.Harness.Fold(EventTypes.TodoChanged, new TodoChanged(_first, 2, "reply to Sam today", Due: null, ClearDue: false, Links: null, "human:ui"));
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("reply to Sam", "reply to Sam later", StringComparison.Ordinal);
        _ = await rig.Pass();
        await rig.Harness.Fold(EventTypes.TodoConflicted, (await rig.Payloads<TodoConflicted>(EventTypes.TodoConflicted)).Single());
        return rig;
    }

    private static string NoteLine(SyncRig rig) => rig.Note!.Split('\n').Single(static l => l.StartsWith("- [", StringComparison.Ordinal));

    private static async Task<long> Head(SyncRig rig) => (await rig.Harness.Slice.Store.Head(TestContext.Current.CancellationToken)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_todo_changed_in_both_places_raises_one_conflict_and_leaves_the_note_line_as_typed()
    {
        var rig = await WithOpenConflict();

        var raised = Assert.Single(await rig.Payloads<TodoConflicted>(EventTypes.TodoConflicted));
        Assert.Equal((_first, "reply to Sam today", "reply to Sam later"), (raised.Id, raised.Sindri.Text, raised.Obsidian.Text));
        Assert.StartsWith("sha256:", raised.NoteHash, StringComparison.Ordinal);
        Assert.Contains("reply to Sam later", rig.Note, StringComparison.Ordinal);
        Assert.Empty(await rig.Events(EventTypes.TodoChanged));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_conflict_with_the_note_line_untouched_appends_nothing()
    {
        var rig = await WithOpenConflict();
        var head = await Head(rig);

        var status = await rig.Pass();

        Assert.Equal((head, TodoNoteState.Syncing), (await Head(rig), status.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_note_line_that_moves_again_raises_a_replacement_conflict()
    {
        var rig = await WithOpenConflict();
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("later", "much later", StringComparison.Ordinal);

        _ = await rig.Pass();

        var raised = await rig.Payloads<TodoConflicted>(EventTypes.TodoConflicted);
        Assert.Equal(["reply to Sam later", "reply to Sam much later"], raised.Select(c => c.Obsidian.Text), StringComparer.Ordinal);
        Assert.Empty(await rig.Events(EventTypes.TodoChanged));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_note_line_edited_to_match_Sindri_settles_the_conflict_for_Sindri_by_the_service()
    {
        var rig = await WithOpenConflict();
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("later", "today", StringComparison.Ordinal);

        _ = await rig.Pass();

        var settled = Assert.Single(await rig.Payloads<TodoConflictResolved>(EventTypes.TodoConflictResolved));
        Assert.Equal((_first, TodoSideName.Sindri, NoteLine(rig), "service:todo-note"), (settled.Id, settled.Kept, settled.BaseLine, settled.By));
        Assert.StartsWith($"todo.conflict.resolved:{_first.Value}:", Assert.Single(await rig.Events(EventTypes.TodoConflictResolved)).Key, StringComparison.Ordinal);
        Assert.Empty(await rig.Events(EventTypes.TodoChanged));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pick_for_Sindri_rewrites_the_note_line_from_Sindri_and_raises_nothing_again()
    {
        var rig = await WithOpenConflict();
        var picked = new TodoConflictResolved(_first, TodoSideName.Sindri, NoteLine(rig), "human:ui");
        await rig.Harness.Record(EventTypes.TodoConflictResolved, picked, "todo.conflict.resolved:" + _first.Value);
        await rig.Harness.Fold(EventTypes.TodoConflictResolved, picked);

        _ = await rig.Pass();

        Assert.Contains("reply to Sam today ^td-", NoteLine(rig), StringComparison.Ordinal);
        _ = Assert.Single(await rig.Events(EventTypes.TodoConflicted));
        Assert.Empty(await rig.Events(EventTypes.TodoChanged));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pick_for_Sindri_against_a_deleted_line_brings_the_line_back()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(_first, "reply to Sam");
        _ = await rig.Pass();
        await rig.Harness.Fold(EventTypes.TodoChanged, new TodoChanged(_first, 2, "reply to Sam today", Due: null, ClearDue: false, Links: null, "human:ui"));
        rig.Files.Files[SyncRig.Path] = "# Todo\n";
        _ = await rig.Pass();
        var raised = Assert.Single(await rig.Payloads<TodoConflicted>(EventTypes.TodoConflicted));
        Assert.True(raised.Obsidian.Removed);
        await rig.Harness.Fold(EventTypes.TodoConflicted, raised);
        var picked = new TodoConflictResolved(_first, TodoSideName.Sindri, BaseLine: string.Empty, "human:ui");
        await rig.Harness.Record(EventTypes.TodoConflictResolved, picked, "todo.conflict.resolved:" + _first.Value);
        await rig.Harness.Fold(EventTypes.TodoConflictResolved, picked);

        _ = await rig.Pass();

        Assert.Contains("- [ ] reply to Sam today ^td-", rig.Note, StringComparison.Ordinal);
        _ = Assert.Single(await rig.Events(EventTypes.TodoConflicted));
        Assert.Empty(await rig.Events(EventTypes.TodoRemoved));
    }
}
