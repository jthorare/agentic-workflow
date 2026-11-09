using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>TP-12: how the sync ends a hold it opened: dissolved when the deletions come back, replaced when the set changes, left alone when it doesn't.</summary>
public sealed class NoteHoldSyncTests
{
    // 20 open todos in the note; lines 2-6 (5 of them, 25%) deleted: more than 3 and more than 10%, so the hold opens
    private static async Task<(SyncRig Rig, string Whole)> HoldOpen()
    {
        var (rig, _) = await SyncRig.WithTodos(20);
        var whole = rig.Note!;
        rig.Files.Files[SyncRig.Path] = SyncRig.WithoutLines(whole, from: 2, to: 7);
        _ = await rig.Pass();
        _ = Assert.Single(await rig.Payloads<TodoNoteHeld>(EventTypes.TodoNoteHeld));
        return (rig, whole);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Deleted_lines_that_come_back_dissolve_the_hold()
    {
        var (rig, whole) = await HoldOpen();
        var held = (await rig.Payloads<TodoNoteHeld>(EventTypes.TodoNoteHeld))[0];
        rig.Files.Files[SyncRig.Path] = whole;

        var status = await rig.Pass();

        var resolved = Assert.Single(await rig.Payloads<TodoNoteHeldResolved>(EventTypes.TodoNoteHeldResolved));
        Assert.Equal(("dissolved", held.NoteHash, 5, "service:todo-note", TodoNoteState.Syncing), (resolved.Kept, resolved.NoteHash, resolved.Count, resolved.By, status.State));
        var key = Assert.Single(await rig.Events(EventTypes.TodoNoteHeldResolved)).Key;
        Assert.StartsWith("todo.note.held.resolved:" + held.NoteHash + ":", key, StringComparison.Ordinal);
        Assert.Empty(await rig.Events(EventTypes.TodoRemoved));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sixth_deleted_line_replaces_the_hold_with_a_new_todo_note_held()
    {
        var (rig, _) = await HoldOpen();
        rig.Files.Files[SyncRig.Path] = SyncRig.WithoutLines(rig.Note!, from: 2, to: 3);

        _ = await rig.Pass();

        var held = await rig.Payloads<TodoNoteHeld>(EventTypes.TodoNoteHeld);
        Assert.Equal([5, 6], held.Select(h => h.Changes.Count));
        Assert.NotEqual(held[0].NoteHash, held[1].NoteHash, StringComparer.Ordinal);
        Assert.Equal("todo.note.held:" + held[1].NoteHash, (await rig.Events(EventTypes.TodoNoteHeld))[1].Key);
        Assert.Empty(await rig.Events(EventTypes.TodoNoteHeldResolved, EventTypes.TodoRemoved));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_deleted_lines_on_the_next_pass_append_nothing()
    {
        var (rig, _) = await HoldOpen();
        var head = (await rig.Harness.Slice.Store.Head(TestContext.Current.CancellationToken)).Value;

        _ = await rig.Pass();

        Assert.Equal(head, (await rig.Harness.Slice.Store.Head(TestContext.Current.CancellationToken)).Value);
    }
}
