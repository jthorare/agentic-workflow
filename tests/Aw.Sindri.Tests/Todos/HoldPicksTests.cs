using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.PickTodoHold;
using Aw.Sindri.Features.UndoTodoHold;

namespace Aw.Sindri.Tests.Todos;

/// <summary>Applying or putting back the note's held changes and the Undo of either (todo spec REQ-E7, §4.1, §4.4, §6).</summary>
public sealed class HoldPicksTests
{
    private const string Hash = "sha256:3f1c";
    private static readonly TodoId _first = TodoHarness.First;
    private static readonly TodoId _second = TodoHarness.Second;
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Line(TodoId id, string text) => $"- [ ] {text} ^td-{id.Value[3..]}";

    // two held changes: the first todo removed in the note, the second edited there
    private async Task Held(bool seedHold = true)
    {
        await _h.Lease();
        // why: the rows sit at sequence 0, before the hold (the first ledger event); a row written later than the hold was changed after it was drawn
        await _h.SeedAt(_first, "reply to Sam", sequence: 0);
        await _h.SeedAt(_second, "call the dentist", sequence: 0);
        if (seedHold)
        {
            await _h.Record(
                EventTypes.TodoNoteHeld,
                new TodoNoteHeld([new TodoHeldChange(_first, "removed", NoteLine: null), new TodoHeldChange(_second, "edited", Line(_second, "call the dentist about the crown"))], Hash, "service:todo-note"),
                "todo.note.held:" + Hash);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Apply_does_the_held_changes_as_ordinary_events_by_obsidian_then_records_the_pick()
    {
        await Held();
        using var cli = _h.Cli();

        var applied = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);

        Assert.True(applied.IsSuccess, applied.IsError ? applied.Error.ToString() : null);
        Assert.Equal(("applied", 2), (applied.Value.Kept, applied.Value.Count));
        Assert.Equal(_h.Slice.Clock.GetUtcNow().AddSeconds(10), applied.Value.UndoUntil);
        var types = _h.Slice.Store.Events.Select(e => e.Envelope.Type).ToList();
        Assert.Equal(["todo.note.held", "todo.removed", "todo.changed", "todo.note.held.resolved"], types, StringComparer.Ordinal);
        Assert.Equal("obsidian", _h.Payload<TodoRemoved>(_h.Slice.Store.Events[1]).By);
        Assert.Equal("call the dentist about the crown", _h.Payload<TodoChanged>(_h.Slice.Store.Events[2]).Text);
        var resolved = _h.Payload<TodoNoteHeldResolved>(_h.Slice.Store.Events[3]);
        Assert.Equal(("applied", Hash, 2, "human:cli"), (resolved.Kept, resolved.NoteHash, resolved.Count, resolved.By));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Put_them_back_records_only_the_pick_because_the_note_is_written_when_the_window_closes()
    {
        await Held();
        using var session = _h.Session();

        var restored = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash, Piped: true), Token);

        Assert.True(restored.IsSuccess, restored.IsError ? restored.Error.ToString() : null);
        Assert.Equal(("restored", 2), (restored.Value.Kept, restored.Value.Count));
        Assert.Equal(["todo.note.held", "todo.note.held.resolved"], _h.Slice.Store.Events.Select(e => e.Envelope.Type), StringComparer.Ordinal);
        var resolved = _h.Payload<TodoNoteHeldResolved>(_h.Slice.Store.Events[1]);
        Assert.StartsWith("session:", resolved.By, StringComparison.Ordinal);
        Assert.Equal("pipe", resolved.Via);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_open_hold_is_SND_TODO_005_and_so_is_a_second_pick_of_the_same_hold()
    {
        await Held(seedHold: false);
        using var cli = _h.Cli();
        Assert.Equal("SND-TODO-005", (await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token)).Error.Code);

        await _h.Record(EventTypes.TodoNoteHeld, new TodoNoteHeld([new TodoHeldChange(_first, "removed", NoteLine: null)], Hash, "service:todo-note"), "todo.note.held:" + Hash);
        Assert.True((await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash), Token)).IsSuccess);

        Assert.Equal("SND-TODO-005", (await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash), Token)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_different_note_hash_is_SND_TODO_409_and_records_nothing_more()
    {
        await Held();
        using var cli = _h.Cli();

        var stale = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, "sha256:other"), Token);

        Assert.Equal("SND-TODO-409", stale.Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_smaller_set_still_true_in_the_note_is_SND_TODO_409_and_says_how_many_are_left()
    {
        await Held();
        // the owner pasted the first todo's line back, so only the edit is still true
        _h.NoteFile.Current = new TodoNoteSnapshot(Hash, NoteParser.Parse(Line(_first, "reply to Sam") + "\n" + Line(_second, "call the dentist about the crown") + "\n"));
        using var cli = _h.Cli();

        var smaller = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);

        Assert.Equal("SND-TODO-409", smaller.Error.Code);
        Assert.Contains("now 1", smaller.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_inside_the_window_reverses_apply_and_reopens_the_hold()
    {
        await Held();
        using var cli = _h.Cli();
        _ = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);
        await _h.Fold(EventTypes.TodoRemoved, new TodoRemoved(_first, 2, "obsidian"));
        await _h.Fold(EventTypes.TodoChanged, new TodoChanged(_second, 2, "call the dentist about the crown", Due: null, ClearDue: false, Links: null, "obsidian"));
        _h.Slice.Clock.Advance(TimeSpan.FromSeconds(9));

        var undone = await _h.UndoHold().Handle(new UndoTodoHoldRequest(), Token);

        Assert.True(undone.IsSuccess, undone.IsError ? undone.Error.ToString() : null);
        Assert.Null(undone.Value.Kept);
        var types = _h.Slice.Store.Events.Select(e => e.Envelope.Type).Skip(4).ToList();
        Assert.Equal(["todo.restored", "todo.changed", "todo.note.held.reopened"], types, StringComparer.Ordinal);
        Assert.Equal("call the dentist", _h.Payload<TodoChanged>(_h.Slice.Store.Events[5]).Text);
        // the hold is open again: a pick works
        Assert.True((await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash), Token)).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_apply_then_its_retry_then_Undo_puts_every_todo_back_as_it_was_before_the_first_try()
    {
        await Held();
        using var cli = _h.Cli();
        // the second todo's v2 is already in the ledger (the view hasn't caught up), so the first try removes the first todo and is refused on the second
        var foreign = new TodoChanged(_second, 2, "call the dentist today", Due: null, ClearDue: false, Links: null, "human:ui");
        await _h.Record(EventTypes.TodoChanged, foreign, TodoWrite.VersionKey(_second, 2));

        var failed = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);

        Assert.Equal("SND-TODO-409", failed.Error.Code);
        Assert.DoesNotContain(_h.Slice.Store.Events, e => e.Envelope.Type == "todo.note.held.resolved");

        // the view catches up with both writes; the retry skips the removed todo and applies the second
        // (the view rows fold at sequence 0 here: this test is about the retry, not about edits after the hold)
        await _h.FoldAt(EventTypes.TodoRemoved, new TodoRemoved(_first, 2, "obsidian"), 0);
        await _h.FoldAt(EventTypes.TodoChanged, foreign, 0);
        var retried = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);
        Assert.True(retried.IsSuccess, retried.IsError ? retried.Error.ToString() : null);
        await _h.FoldAt(EventTypes.TodoChanged, new TodoChanged(_second, 3, "call the dentist about the crown", Due: null, ClearDue: false, Links: null, "obsidian"), 0);

        var undone = await _h.UndoHold().Handle(new UndoTodoHoldRequest(), Token);

        Assert.True(undone.IsSuccess, undone.IsError ? undone.Error.ToString() : null);
        var undoTypes = _h.Slice.Store.Events.Select(e => e.Envelope.Type).TakeLast(3).ToList();
        Assert.Equal(["todo.restored", "todo.changed", "todo.note.held.reopened"], undoTypes, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Put_them_back_after_a_failed_apply_returns_every_todo_the_failed_try_changed_to_before_that_try()
    {
        await Held();
        using var cli = _h.Cli();
        var foreign = new TodoChanged(_second, 2, "call the dentist today", Due: null, ClearDue: false, Links: null, "human:ui");
        await _h.Record(EventTypes.TodoChanged, foreign, TodoWrite.VersionKey(_second, 2));
        var failed = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);
        Assert.Equal("SND-TODO-409", failed.Error.Code);
        // the view catches up with both writes (rows fold at sequence 0: this test is about the restore, not about edits after the hold)
        await _h.FoldAt(EventTypes.TodoRemoved, new TodoRemoved(_first, 2, "obsidian"), 0);
        await _h.FoldAt(EventTypes.TodoChanged, foreign, 0);

        var restored = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash), Token);

        Assert.True(restored.IsSuccess, restored.IsError ? restored.Error.ToString() : null);
        // the first todo, which the failed try removed, is back (version 3); the second was never touched by the try, so it gets no event
        var after = _h.Slice.Store.Events.Select(e => e.Envelope.Type).TakeLast(2).ToList();
        Assert.Equal(["todo.restored", "todo.note.held.resolved"], after, StringComparer.Ordinal);
        var back = _h.Payload<TodoRestored>(_h.Slice.Store.Events[^2]);
        Assert.Equal((_first, 3L), (back.Id, back.Version));
        Assert.DoesNotContain(_h.Slice.Store.Events.Skip(3), e => e.Envelope.Type == "todo.changed");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Put_them_back_while_the_views_lag_the_failed_apply_never_answers_restored_for_a_todo_it_did_not_revert()
    {
        await Held();
        using var cli = _h.Cli();
        var foreign = new TodoChanged(_second, 2, "call the dentist today", Due: null, ClearDue: false, Links: null, "human:ui");
        await _h.Record(EventTypes.TodoChanged, foreign, TodoWrite.VersionKey(_second, 2));
        var failed = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);
        Assert.Equal("SND-TODO-409", failed.Error.Code);

        // the views have not folded the failed try's removal of the first todo (the row is still at version 1), so the todo can't be told apart from one that moved since
        var lagging = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash), Token);

        Assert.True(lagging.IsError, "restore answered success while the first todo was never put back");
        Assert.DoesNotContain(_h.Slice.Store.Events, e => e.Envelope.Type == "todo.note.held.resolved");

        // once the views catch up, the same restore puts the first todo back
        await _h.FoldAt(EventTypes.TodoRemoved, new TodoRemoved(_first, 2, "obsidian"), 0);
        await _h.FoldAt(EventTypes.TodoChanged, foreign, 0);
        var restored = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash), Token);
        Assert.True(restored.IsSuccess, restored.IsError ? restored.Error.ToString() : null);
        Assert.Contains(_h.Slice.Store.Events, e => e.Envelope.Type == "todo.restored");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_while_the_views_lag_the_apply_is_SND_API_011_and_reopens_nothing_until_they_catch_up()
    {
        await Held();
        using var cli = _h.Cli();
        _ = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);

        // the views have not folded the apply (both rows still at version 1), so Undo can't tell "not folded yet" from "moved since"
        var lagging = await _h.UndoHold().Handle(new UndoTodoHoldRequest(), Token);

        Assert.Equal("SND-API-011", lagging.Error.Code);
        Assert.DoesNotContain(_h.Slice.Store.Events, e => string.Equals(e.Envelope.Type, "todo.note.held.reopened", StringComparison.Ordinal) || string.Equals(e.Envelope.Type, "todo.restored", StringComparison.Ordinal));

        // once the views catch up, the same Undo reverses the apply and reopens the hold
        await _h.FoldAt(EventTypes.TodoRemoved, new TodoRemoved(_first, 2, "obsidian"), 0);
        await _h.FoldAt(EventTypes.TodoChanged, new TodoChanged(_second, 2, "call the dentist about the crown", Due: null, ClearDue: false, Links: null, "obsidian"), 0);
        var undone = await _h.UndoHold().Handle(new UndoTodoHoldRequest(), Token);

        Assert.True(undone.IsSuccess, undone.IsError ? undone.Error.ToString() : null);
        var types = _h.Slice.Store.Events.Select(e => e.Envelope.Type).TakeLast(3).ToList();
        Assert.Equal(["todo.restored", "todo.changed", "todo.note.held.reopened"], types, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_todo_changed_in_sindri_after_the_hold_is_conflicted_not_overwritten_and_the_others_apply()
    {
        await Held();
        // the second todo was edited in Sindri after the hold was drawn (the ledger is at sequence 1 for the hold; this edit is later)
        await _h.FoldAt(EventTypes.TodoChanged, new TodoChanged(_second, 2, "call the dentist today", Due: null, ClearDue: false, Links: null, "human:ui"), 50);
        using var cli = _h.Cli();

        var applied = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: false, Hash), Token);

        Assert.True(applied.IsSuccess, applied.IsError ? applied.Error.ToString() : null);
        Assert.Equal(["todo.note.held", "todo.removed", "todo.conflicted", "todo.note.held.resolved"], _h.Slice.Store.Events.Select(e => e.Envelope.Type), StringComparer.Ordinal);
        var conflicted = _h.Payload<TodoConflicted>(_h.Slice.Store.Events[2]);
        Assert.Equal((_second, "call the dentist today", "call the dentist about the crown", Hash), (conflicted.Id, conflicted.Sindri.Text, conflicted.Obsidian.Text, conflicted.NoteHash));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_after_the_window_or_with_nothing_picked_is_SND_TODO_005()
    {
        await Held();
        using var cli = _h.Cli();
        Assert.Equal("SND-TODO-005", (await _h.UndoHold().Handle(new UndoTodoHoldRequest(), Token)).Error.Code);

        _ = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash), Token);
        _h.Slice.Clock.Advance(TimeSpan.FromSeconds(11));

        Assert.Equal("SND-TODO-005", (await _h.UndoHold().Handle(new UndoTodoHoldRequest(), Token)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_window_lists_what_is_still_open_for_the_note_writer()
    {
        await Held();
        using var cli = _h.Cli();
        _ = await _h.PickHold().Handle(new PickTodoHoldRequest(Restore: true, Hash), Token);

        Assert.True(_h.Picks.Open(_h.Slice.Clock.GetUtcNow()).Hold);
        _h.Slice.Clock.Advance(TimeSpan.FromSeconds(11));
        Assert.False(_h.Picks.Open(_h.Slice.Clock.GetUtcNow()).Hold);
    }
}
