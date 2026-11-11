using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.GetTodoNote;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The todo reads (todo spec §4.4): the list by state with counts, one todo, and the note answering 'not set'.</summary>
public sealed class TodoReadsTests
{
    private static readonly TodoId _open = TodoHarness.First;
    private static readonly TodoId _done = TodoHarness.Second;
    private static readonly TodoId _gone = TodoId.From("td_01jc5c90a2b7k4m8q1r6t3w9xz");
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task SeedThree()
    {
        await _h.Seed(_open, "open one");
        await _h.Seed(_done, "done one");
        await _h.Fold(EventTypes.TodoDone, new TodoDone(_done, Version: 2, By: "human:ui"));
        await _h.Seed(_gone, "removed one");
        await _h.Fold(EventTypes.TodoRemoved, new TodoRemoved(_gone, Version: 2, By: "human:ui"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(TodoListState.Open, new[] { "open one" })]
    [InlineData(TodoListState.Done, new[] { "done one" })]
    [InlineData(TodoListState.All, new[] { "open one", "done one", "removed one" })]
    public async Task The_list_holds_the_todos_of_the_asked_state_with_the_counts_of_every_state(TodoListState state, string[] texts)
    {
        await SeedThree();
        using var session = _h.Session();

        var list = await _h.List().Handle(new ListTodosRequest(state), Token);

        Assert.True(list.IsSuccess, list.IsError ? list.Error.ToString() : null);
        Expect.Sequence(texts, list.Value.Todos.Select(t => t.Text));
        Assert.Equal((1, 1, 1), (list.Value.Counts.Open, list.Value.Counts.Done, list.Value.Counts.Removed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_default_state_is_open()
    {
        await SeedThree();
        using var cli = _h.Cli();

        var list = await _h.List().Handle(new ListTodosRequest(), Token);

        Assert.True(list.IsSuccess, list.IsError ? list.Error.ToString() : null);
        Expect.Sequence(["open one"], list.Value.Todos.Select(t => t.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_todo_is_returned_with_its_open_conflict()
    {
        await _h.Seed(_open);
        var side = new TodoSide("x", Due: null, Links: [], Done: false, Removed: false);
        await _h.Fold(EventTypes.TodoConflicted, new TodoConflicted(_open, side, side with { Text = "y" }, NoteHash: "ab12"));
        using var cli = _h.Cli();

        var got = await _h.Get().Handle(new GetTodoRequest(_open), Token);

        Assert.True(got.IsSuccess, got.IsError ? got.Error.ToString() : null);
        Assert.Equal(("y", "x"), (got.Value.Todo.Conflict!.Obsidian.Text, got.Value.Todo.Conflict.Sindri.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_todo_answers_the_TodoItem_wire_record_with_link_objects()
    {
        await _h.Seed(_open);
        using var cli = _h.Cli();

        var got = await _h.Get().Handle(new GetTodoRequest(_open), Token);

        Assert.True(got.IsSuccess, got.IsError ? got.Error.ToString() : null);
        _ = Assert.IsType<TodoItem>(got.Value.Todo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_todo_is_SND_TODO_404()
    {
        using var cli = _h.Cli();

        var missing = await _h.Get().Handle(new GetTodoRequest(_open), Token);

        Assert.True(missing.IsError);
        Assert.Equal("SND-TODO-404", missing.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_note_answers_not_set_until_a_note_is_set()
    {
        using var cli = _h.Cli();

        var note = await _h.Note().Handle(new GetTodoNoteRequest(), Token);

        Assert.True(note.IsSuccess, note.IsError ? note.Error.ToString() : null);
        Assert.Equal("not set", note.Value.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_note_answers_the_syncs_status_the_last_sync_and_the_open_conflicts_once_a_key_is_set()
    {
        using var cli = _h.Cli();
        await _h.Lease();
        _h.NoteLocator.Place = new NotePlace("notes", "Todo.md", "/vault");
        var checkedAt = _h.Slice.Clock.GetUtcNow();
        _h.NoteSync.Status = new TodoNoteStatus(TodoNoteState.CantRead, checkedAt, "the note is over 1 MB", FailedInARow: 3, NotAdopted: 0);
        await _h.Record(EventTypes.TodoNoteSynced, new TodoNoteSynced("notes", "Todo.md", "h", "b", 1, 1, "service:todo-note"), "todo.note.synced:1");
        await _h.Seed(_open);
        var side = new TodoSide("x", Due: null, Links: [], Done: false, Removed: false);
        await _h.Fold(EventTypes.TodoConflicted, new TodoConflicted(_open, side, side with { Text = "y" }, NoteHash: "ab12"));

        var note = await _h.Note().Handle(new GetTodoNoteRequest(), Token);

        Assert.True(note.IsSuccess, note.IsError ? note.Error.ToString() : null);
        Assert.Equal(("can't read", "notes", "Todo.md", checkedAt, "the note is over 1 MB"), (note.Value.State, note.Value.Vault, note.Value.Note, note.Value.LastCheckAt, note.Value.LastError));
        _ = Assert.NotNull(note.Value.LastSyncAt);
        Assert.Equal([_open.Value], note.Value.Conflicts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_that_has_not_had_a_pass_in_this_process_reads_as_syncing()
    {
        using var cli = _h.Cli();
        _h.NoteLocator.Place = new NotePlace("notes", "Todo.md", "/vault");

        var note = await _h.Note().Handle(new GetTodoNoteRequest(), Token);

        Assert.Equal("syncing", note.Value.State);
    }
}
