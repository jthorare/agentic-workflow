using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>REQ-E7: "Put them back in the note" closes the hold and the sync writes the held lines back, but only after the pick's 10 s Undo window.</summary>
public sealed class NoteHoldPickSyncTests
{
    // 20 todos synced; the owner changes 5 note lines (lines 2-6) so the hold opens; the pick is "Put them back"
    private static async Task<(SyncRig Rig, TodoNoteHeld Held, int Swaps)> PutBack(Func<string, string> changeNote)
    {
        var (rig, _) = await SyncRig.WithTodos(20);
        rig.Files.Files[SyncRig.Path] = changeNote(rig.Note!);
        _ = await rig.Pass();
        var held = Assert.Single(await rig.Payloads<TodoNoteHeld>(EventTypes.TodoNoteHeld));
        var resolved = new TodoNoteHeldResolved("restored", held.NoteHash, held.Changes.Count, "human:cli");
        await rig.Harness.Record(EventTypes.TodoNoteHeldResolved, resolved, "todo.note.held.resolved:" + held.NoteHash);
        rig.Harness.Picks.RememberHold(new TodoHoldPick(rig.Harness.Slice.Clock.GetUtcNow(), "restored", []));
        return (rig, held, rig.Files.Swaps);
    }

    private static void CloseWindow(SyncRig rig) => rig.Harness.Slice.Clock.Advance(TodoPickWindow.Span + TimeSpan.FromSeconds(1));

    [Fact, Trait("Lane", "unit")]
    public async Task Deleted_lines_stay_out_of_the_note_for_the_window_and_come_back_after_it()
    {
        var (rig, held, swaps) = await PutBack(note => SyncRig.WithoutLines(note, from: 2, to: 7));

        _ = await rig.Pass();

        Assert.Equal(swaps, rig.Files.Swaps);
        CloseWindow(rig);

        _ = await rig.Pass();

        foreach (var change in held.Changes)
        {
            Assert.Contains($"^td-{change.Id.Value["td_".Length..]}", rig.Note, StringComparison.Ordinal);
        }

        Assert.Empty(await rig.Events(EventTypes.TodoRemoved));
        _ = Assert.Single(await rig.Events(EventTypes.TodoNoteHeld));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edited_lines_keep_the_owners_text_for_the_window_and_are_written_from_Sindri_after_it()
    {
        var (rig, _, swaps) = await PutBack(note => string.Join('\n', note.Split('\n').Select((l, i) => i is >= 2 and < 7 ? l.Replace("todo ", "edited ", StringComparison.Ordinal) : l)));

        _ = await rig.Pass();

        Assert.Equal((swaps, 5), (rig.Files.Swaps, rig.Note!.Split("edited ").Length - 1));
        CloseWindow(rig);

        _ = await rig.Pass();

        Assert.DoesNotContain("edited ", rig.Note, StringComparison.Ordinal);
        Assert.Empty(await rig.Events(EventTypes.TodoChanged));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_Undo_of_the_pick_reopens_the_hold_and_the_lines_stay_out()
    {
        var (rig, held, swaps) = await PutBack(note => SyncRig.WithoutLines(note, from: 2, to: 7));
        await rig.Harness.Record(EventTypes.TodoNoteHeldReopened, new TodoNoteHeldReopened("human:ui"), "todo.note.held.reopened:" + held.NoteHash);
        rig.Harness.Picks.ForgetHold();

        _ = await rig.Pass();

        Assert.Equal(swaps, rig.Files.Swaps);
        Assert.Empty(await rig.Events(EventTypes.TodoRemoved));
    }
}
