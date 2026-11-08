using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.MarkTodo;

namespace Aw.Sindri.Tests.Todos;

/// <summary>
/// Two writers on one version (todo spec D9): both read the row at v1 before either write folds, so the version key is the only guard.
/// Every state change shares one key per todo and version, so exactly one wins and the other is SND-TODO-409, whatever the verbs.
/// </summary>
public sealed class TodoRaceTests
{
    private static readonly TodoId _id = TodoHarness.First;
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Done_and_remove_at_one_version_record_one_event_and_the_second_is_SND_TODO_409()
    {
        await _h.Lease();
        await _h.Seed(_id);
        using var cli = _h.Cli();

        var done = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Done, Version: 1), Token);
        var removed = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Remove, Version: 1), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.ToString() : null);
        Assert.True(removed.IsError);
        Assert.Equal("SND-TODO-409", removed.Error.Code);
        Assert.Equal($"todo:{_id.Value}:v2", Assert.Single(_h.Slice.Store.Events).IdempotencyKey);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_change_and_a_mark_at_one_version_record_one_event()
    {
        await _h.Lease();
        await _h.Seed(_id);
        using var cli = _h.Cli();

        var changed = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, Text: "edit"), Token);
        var done = await _h.Mark().Handle(new MarkTodoRequest(_id, TodoVerb.Done, Version: 1), Token);

        Assert.True(changed.IsSuccess, changed.IsError ? changed.Error.ToString() : null);
        Assert.Equal("SND-TODO-409", done.Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_different_changes_at_one_version_keep_the_first_and_refuse_the_second()
    {
        await _h.Lease();
        await _h.Seed(_id);
        using var cli = _h.Cli();

        var first = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, Text: "first edit"), Token);
        var second = await _h.Change().Handle(new ChangeTodoRequest(_id, Version: 1, Text: "second edit"), Token);

        Assert.True(first.IsSuccess, first.IsError ? first.Error.ToString() : null);
        Assert.True(second.IsError);
        Assert.Equal("SND-TODO-409", second.Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }
}
