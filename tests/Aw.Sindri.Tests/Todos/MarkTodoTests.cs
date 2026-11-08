using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.MarkTodo;

namespace Aw.Sindri.Tests.Todos;

/// <summary>Done, reopen, remove and restore (todo spec §4.1, D8, D9, §6): soft state changes, any caller, version optional.</summary>
public sealed class MarkTodoTests
{
    private static readonly TodoId _id = TodoHarness.First;
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // the state each verb starts from
    private async Task SeedFor(TodoVerb verb)
    {
        await _h.Seed(_id);
        if (verb == TodoVerb.Reopen)
        {
            await _h.Fold(EventTypes.TodoDone, new TodoDone(_id, Version: 2, By: "human:ui"));
        }

        if (verb == TodoVerb.Restore)
        {
            await _h.Fold(EventTypes.TodoRemoved, new TodoRemoved(_id, Version: 2, By: "human:ui"));
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(TodoVerb.Done, "todo.done")]
    [InlineData(TodoVerb.Reopen, "todo.reopened")]
    [InlineData(TodoVerb.Remove, "todo.removed")]
    [InlineData(TodoVerb.Restore, "todo.restored")]
    public async Task Each_verb_records_its_event_at_the_next_version(TodoVerb verb, string type)
    {
        await _h.Lease();
        await SeedFor(verb);
        var seen = verb is TodoVerb.Reopen or TodoVerb.Restore ? 2L : 1L;
        using var cli = _h.Cli();

        var marked = await _h.Mark().Handle(new MarkTodoRequest(_id, verb, Version: seen), Token);

        Assert.True(marked.IsSuccess, marked.IsError ? marked.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Assert.Equal(type, recorded.Envelope.Type);
        Assert.Equal($"{type}:{_id.Value}:v{TodoHarness.Num(seen + 1)}", recorded.IdempotencyKey);
        Assert.Equal((_id, seen + 1), (marked.Value.Id, marked.Value.Version));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_version_the_current_one_is_used_and_the_caller_is_recorded()
    {
        await _h.Lease();
        await _h.Seed(_id);
        using var session = _h.Session();

        var done = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Done), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.ToString() : null);
        var payload = _h.Payload<TodoDone>(Assert.Single(_h.Slice.Store.Events));
        Assert.Equal(2L, payload.Version);
        Assert.StartsWith("session:", payload.By, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_version_is_SND_TODO_409_and_records_nothing()
    {
        await _h.Lease();
        await _h.Seed(_id);
        await _h.Fold(EventTypes.TodoChanged, new TodoChanged(_id, Version: 2, Text: "edited elsewhere", Due: null, ClearDue: false, Links: null, By: "human:ui"));
        using var cli = _h.Cli();

        var stale = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Done, Version: 1), Token);

        Assert.True(stale.IsError);
        Assert.Equal("SND-TODO-409", stale.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_todo_with_an_open_conflict_is_SND_TODO_423()
    {
        await _h.Lease();
        await _h.Seed(_id);
        var side = new TodoSide("x", Due: null, Links: [], Done: false, Removed: false);
        await _h.Fold(EventTypes.TodoConflicted, new TodoConflicted(_id, side, side with { Done = true }, NoteHash: "ab12"));
        using var cli = _h.Cli();

        var locked = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Done), Token);

        Assert.True(locked.IsError);
        Assert.Equal("SND-TODO-423", locked.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_todo_is_SND_TODO_404()
    {
        await _h.Lease();
        using var cli = _h.Cli();

        var unknown = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Done), Token);

        Assert.True(unknown.IsError);
        Assert.Equal("SND-TODO-404", unknown.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launched_session_may_remove_and_the_owner_may_restore()
    {
        await _h.Lease();
        await _h.Seed(_id);

        using (_h.Launched())
        {
            var removed = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Remove), Token);
            Assert.True(removed.IsSuccess, removed.IsError ? removed.Error.ToString() : null);
        }

        await _h.Fold(EventTypes.TodoRemoved, new TodoRemoved(_id, Version: 2, By: "session:cli:launch-1"));
        using (_h.Cli())
        {
            var restored = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Restore), Token);
            Assert.True(restored.IsSuccess, restored.IsError ? restored.Error.ToString() : null);
        }
    }
}
