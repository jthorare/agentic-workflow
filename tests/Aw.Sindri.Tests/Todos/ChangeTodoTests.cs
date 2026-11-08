using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.ChangeTodo;

namespace Aw.Sindri.Tests.Todos;

/// <summary>Editing a todo (todo spec §4.1, D9, §6): needs the version, refuses a todo with an open conflict.</summary>
public sealed class ChangeTodoTests
{
    private static readonly TodoId _id = TodoHarness.First;
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_change_records_the_asked_fields_at_the_next_version()
    {
        await _h.Lease();
        await _h.Seed(_id, due: new DateOnly(2026, 11, 13));
        using var cli = _h.Cli();

        var changed = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, Text: "reply to Sam about the offsite", Links: ["ENG-12"]), Token);

        Assert.True(changed.IsSuccess, changed.IsError ? changed.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Payload<TodoChanged>(recorded);
        Assert.Equal($"todo.changed:{_id.Value}:v2", recorded.IdempotencyKey);
        Assert.Equal((2L, "reply to Sam about the offsite", null, false, "human:cli"), (payload.Version, payload.Text, payload.Due, payload.ClearDue, payload.By));
        Assert.Equal(["sindri://linear/ENG-12"], payload.Links);
        Assert.Equal((_id, 2L), (changed.Value.Id, changed.Value.Version));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ClearDue_drops_the_date_and_an_empty_link_list_drops_the_links()
    {
        await _h.Lease();
        await _h.Seed(_id, due: new DateOnly(2026, 11, 13));
        using var cli = _h.Cli();

        var changed = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, ClearDue: true, Links: []), Token);

        Assert.True(changed.IsSuccess, changed.IsError ? changed.Error.ToString() : null);
        var payload = _h.Payload<TodoChanged>(Assert.Single(_h.Slice.Store.Events));
        Assert.True(payload.ClearDue);
        Assert.Empty(payload.Links!);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_version_is_SND_TODO_409_and_records_nothing()
    {
        await _h.Lease();
        await _h.Seed(_id);
        await _h.Fold(EventTypes.TodoDone, new TodoDone(_id, Version: 2, By: "human:ui"));
        using var cli = _h.Cli();

        var stale = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, Text: "late edit"), Token);

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
        await _h.Fold(EventTypes.TodoConflicted, new TodoConflicted(_id, side, side with { Text = "y" }, NoteHash: "ab12"));
        using var cli = _h.Cli();

        var locked = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, Text: "edit"), Token);

        Assert.True(locked.IsError);
        Assert.Equal("SND-TODO-423", locked.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_todo_is_SND_TODO_404_and_bad_text_is_SND_TODO_007()
    {
        await _h.Lease();
        await _h.Seed(_id);
        using var cli = _h.Cli();

        var unknown = await _h.Change().Handle(new ChangeTodoRequest(TodoHarness.Second, Version: 1, Text: "x"), Token);
        var bad = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, Text: "see [plan](https://example.com)"), Token);

        Assert.Equal(["SND-TODO-404", "SND-TODO-007"], [unknown.Error.Code, bad.Error.Code]);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_and_a_launched_session_may_change_a_todo()
    {
        await _h.Lease();
        await _h.Seed(_id);

        using (_h.Session())
        {
            var bySession = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, Text: "edited by a session"), Token);
            Assert.True(bySession.IsSuccess, bySession.IsError ? bySession.Error.ToString() : null);
        }

        await _h.Fold(EventTypes.TodoChanged, new TodoChanged(_id, Version: 2, Text: "edited by a session", Due: null, ClearDue: false, Links: null, By: "session:mcp"));
        using (_h.Launched())
        {
            var byLaunched = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 2, Text: "edited by a launched session"), Token);
            Assert.True(byLaunched.IsSuccess, byLaunched.IsError ? byLaunched.Error.ToString() : null);
        }
    }
}
