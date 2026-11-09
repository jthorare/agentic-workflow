using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.KeepTodo;
using Aw.Sindri.Features.KeepTodoUndo;

namespace Aw.Sindri.Tests.Todos;

/// <summary>Picking a side of a both-places conflict and its Undo (todo spec D11, §4.1, §4.4, §6): 'todo.conflict.resolved', Obsidian's version as ordinary events by 'obsidian', the 10 s window on Sindri's clock.</summary>
public sealed class KeepTodoTests
{
    private static readonly TodoId _id = TodoHarness.First;
    private static readonly TodoSide _mine = new("call the dentist", Due: null, Links: [], Done: false, Removed: false);
    private static readonly TodoSide _theirs = new("call the dentist about the crown", Due: new DateOnly(2026, 1, 5), Links: [], Done: false, Removed: false);
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task Conflicted(TodoSide? obsidian = null)
    {
        await _h.Lease();
        await _h.Seed(_id, "call the dentist");
        await _h.Fold(EventTypes.TodoConflicted, new TodoConflicted(_id, _mine, obsidian ?? _theirs, NoteHash: "sha256:aa"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Keep_sindri_records_one_resolved_event_by_the_caller_and_opens_the_undo_window()
    {
        await Conflicted();
        using var cli = _h.Cli();

        var kept = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Sindri), Token);

        Assert.True(kept.IsSuccess, kept.IsError ? kept.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Assert.Equal("todo.conflict.resolved", recorded.Envelope.Type);
        var payload = _h.Payload<TodoConflictResolved>(recorded);
        Assert.Equal((TodoSideName.Sindri, "human:cli", null), (payload.Kept, payload.By, payload.Via));
        Assert.Equal(_h.Slice.Clock.GetUtcNow().AddSeconds(10), kept.Value.UndoUntil);
        Assert.Null(kept.Value.Todo.Conflict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Keep_obsidian_then_follows_with_the_ordinary_change_by_obsidian()
    {
        await Conflicted();
        using var cli = _h.Cli();

        var kept = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Obsidian, Piped: true), Token);

        Assert.True(kept.IsSuccess, kept.IsError ? kept.Error.ToString() : null);
        Assert.Equal(["todo.changed", "todo.conflict.resolved"], _h.Slice.Store.Events.Select(e => e.Envelope.Type), StringComparer.Ordinal);
        var changed = _h.Payload<TodoChanged>(_h.Slice.Store.Events[0]);
        Assert.Equal((2L, "call the dentist about the crown", new DateOnly(2026, 1, 5), "obsidian", "pipe"), (changed.Version, changed.Text, changed.Due, changed.By, changed.Via));
        Assert.Equal(("call the dentist about the crown", 2L), (kept.Value.Todo.Text, kept.Value.Todo.Version));
        Assert.Equal("pipe", _h.Payload<TodoConflictResolved>(_h.Slice.Store.Events[1]).Via);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Keep_obsidian_whose_write_is_refused_leaves_the_conflict_open_and_a_retry_works()
    {
        await Conflicted();
        using var cli = _h.Cli();
        // a ChangeTodo landed in the ledger after the view row was read: the converger's todo:X:v2 append is refused
        await _h.Record(EventTypes.TodoChanged, new TodoChanged(_id, 2, "call the dentist today", Due: null, ClearDue: false, Links: null, "human:ui"), TodoWrite.VersionKey(_id, 2));

        var refused = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Obsidian), Token);

        Assert.Equal("SND-TODO-409", refused.Error.Code);
        Assert.DoesNotContain(_h.Slice.Store.Events, e => e.Envelope.Type == "todo.conflict.resolved");

        // the view catches up; the retry is a normal pick, not SND-TODO-005
        await _h.Fold(EventTypes.TodoChanged, new TodoChanged(_id, 2, "call the dentist today", Due: null, ClearDue: false, Links: null, "human:ui"));
        var retried = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Obsidian), Token);

        Assert.True(retried.IsSuccess, retried.IsError ? retried.Error.ToString() : null);
        Assert.Contains(_h.Slice.Store.Events, e => e.Envelope.Type == "todo.conflict.resolved");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Keep_obsidian_on_a_removed_side_removes_the_todo()
    {
        await Conflicted(new TodoSide(Text: null, Due: null, Links: [], Done: false, Removed: true));
        using var cli = _h.Cli();

        var kept = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Obsidian), Token);

        Assert.True(kept.IsSuccess, kept.IsError ? kept.Error.ToString() : null);
        Assert.Equal(["todo.removed", "todo.conflict.resolved"], _h.Slice.Store.Events.Select(e => e.Envelope.Type), StringComparer.Ordinal);
        Assert.True(kept.Value.Todo.Removed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_and_the_app_may_both_pick_and_the_event_says_who()
    {
        await Conflicted();
        using (var session = _h.Session())
        {
            _ = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Sindri), Token);
        }

        Assert.StartsWith("session:", _h.Payload<TodoConflictResolved>(Assert.Single(_h.Slice.Store.Events)).By, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_todo_is_SND_TODO_404()
    {
        await _h.Lease();
        using var cli = _h.Cli();

        var unknown = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Sindri), Token);

        Assert.Equal("SND-TODO-404", unknown.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_todo_with_no_conflict_is_SND_TODO_005()
    {
        await _h.Lease();
        await _h.Seed(_id);
        using var cli = _h.Cli();

        var none = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Sindri), Token);

        Assert.Equal("SND-TODO-005", none.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Keep_obsidian_is_SND_TODO_409_when_the_notes_line_moved_since_and_records_nothing()
    {
        await Conflicted();
        _h.NoteFile.Current = new TodoNoteSnapshot("sha256:bb", NoteParser.Parse($"- [ ] call the dentist on monday ^td-{_id.Value[3..]}\n"));
        using var cli = _h.Cli();

        var moved = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Obsidian), Token);

        Assert.Equal("SND-TODO-409", moved.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Keep_obsidian_goes_through_when_the_notes_line_is_still_the_one_shown()
    {
        await Conflicted();
        _h.NoteFile.Current = new TodoNoteSnapshot("sha256:aa", NoteParser.Parse($"- [ ] call the dentist about the crown \U0001F4C5 2026-01-05 ^td-{_id.Value[3..]}\n"));
        using var cli = _h.Cli();

        var kept = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Obsidian), Token);

        Assert.True(kept.IsSuccess, kept.IsError ? kept.Error.ToString() : null);
        Assert.StartsWith("- [ ] call the dentist about the crown", _h.Payload<TodoConflictResolved>(_h.Slice.Store.Events[1]).BaseLine, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_inside_the_window_reverses_the_applied_events_and_reopens_the_conflict()
    {
        await Conflicted();
        using var cli = _h.Cli();
        _ = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Obsidian), Token);
        await _h.Fold(EventTypes.TodoConflictResolved, new TodoConflictResolved(_id, TodoSideName.Obsidian, string.Empty, "human:cli"));
        await _h.Fold(EventTypes.TodoChanged, new TodoChanged(_id, 2, "call the dentist about the crown", new DateOnly(2026, 1, 5), ClearDue: false, Links: null, "obsidian"));
        _h.Slice.Clock.Advance(TimeSpan.FromSeconds(9));

        var undone = await _h.KeepUndo().Handle(new KeepTodoUndoRequest(_id), Token);

        Assert.True(undone.IsSuccess, undone.IsError ? undone.Error.ToString() : null);
        Assert.Equal(["todo.changed", "todo.conflict.resolved", "todo.changed", "todo.conflict.reopened"], _h.Slice.Store.Events.Select(e => e.Envelope.Type), StringComparer.Ordinal);
        var back = _h.Payload<TodoChanged>(_h.Slice.Store.Events[2]);
        Assert.Equal((3L, "call the dentist", true), (back.Version, back.Text, back.ClearDue));
        Assert.Equal("human:cli", _h.Payload<TodoConflictReopened>(_h.Slice.Store.Events[3]).By);
        Assert.NotNull(undone.Value.Todo.Conflict);
        Assert.Equal("call the dentist", undone.Value.Todo.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_after_the_ten_seconds_is_SND_TODO_005()
    {
        await Conflicted();
        using var cli = _h.Cli();
        _ = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Sindri), Token);
        await _h.Fold(EventTypes.TodoConflictResolved, new TodoConflictResolved(_id, TodoSideName.Sindri, string.Empty, "human:cli"));
        _h.Slice.Clock.Advance(TimeSpan.FromSeconds(11));

        var late = await _h.KeepUndo().Handle(new KeepTodoUndoRequest(_id), Token);

        Assert.Equal("SND-TODO-005", late.Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_with_no_pick_is_005_an_unknown_todo_404_and_a_todo_that_moved_409()
    {
        await _h.Lease();
        await _h.Seed(_id);
        using var cli = _h.Cli();

        Assert.Equal("SND-TODO-005", (await _h.KeepUndo().Handle(new KeepTodoUndoRequest(_id), Token)).Error.Code);
        Assert.Equal("SND-TODO-404", (await _h.KeepUndo().Handle(new KeepTodoUndoRequest(TodoHarness.Second), Token)).Error.Code);

        await _h.Fold(EventTypes.TodoConflicted, new TodoConflicted(_id, _mine, _theirs, NoteHash: "sha256:aa"));
        _ = await _h.Keep().Handle(new KeepTodoRequest(_id, TodoSideName.Sindri), Token);
        await _h.Fold(EventTypes.TodoConflictResolved, new TodoConflictResolved(_id, TodoSideName.Sindri, string.Empty, "human:cli"));
        await _h.Fold(EventTypes.TodoDone, new TodoDone(_id, 2, "human:ui"));

        Assert.Equal("SND-TODO-409", (await _h.KeepUndo().Handle(new KeepTodoUndoRequest(_id), Token)).Error.Code);
    }
}
