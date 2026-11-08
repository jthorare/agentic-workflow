using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Todos;

/// <summary>Adding a todo (todo spec §4.1, §4.4, §6, D2, D3): 'todo.added', keyed for retry, bounded by the open cap and the hourly limit.</summary>
public sealed class AddTodoTests
{
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task An_add_records_the_todo_with_who_wrote_it_keyed_by_the_client_id()
    {
        await _h.Lease();
        using var cli = _h.Cli();

        var added = await _h.Add().Handle(new AddTodoRequest("rotate the staging key", new DateOnly(2026, 11, 10), ["ENG-12"], TodoClientId.From("c-1")), Token);

        Assert.True(added.IsSuccess, added.IsError ? added.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Payload<TodoAdded>(recorded);
        Assert.Equal(EventTypes.TodoAdded, recorded.Envelope.Type);
        Assert.Equal("todo.added:client:c-1", recorded.IdempotencyKey);
        Assert.Equal(("rotate the staging key", new DateOnly(2026, 11, 10), "human:cli"), (payload.Text, payload.Due, payload.By));
        Assert.Equal(["sindri://linear/ENG-12"], payload.Links);
        Assert.Equal((payload.Id, 1L, new DateOnly(2026, 11, 10)), (added.Value.Id, added.Value.Version, added.Value.Due));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_client_id_the_key_is_the_new_todo_id()
    {
        await _h.Lease();
        using var cli = _h.Cli();

        var added = await _h.Add().Handle(new AddTodoRequest("call the dentist"), Token);

        Assert.True(added.IsSuccess, added.IsError ? added.Error.ToString() : null);
        Assert.Equal($"todo.added:{added.Value.Id.Value}", Assert.Single(_h.Slice.Store.Events).IdempotencyKey);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_retried_add_with_the_same_client_id_returns_the_same_todo_and_records_one_event()
    {
        await _h.Lease();
        using var cli = _h.Cli();
        var request = new AddTodoRequest("rotate the staging key", ClientId: TodoClientId.From("c-1"));

        var first = await _h.Add().Handle(request, Token);
        var retry = await _h.Add().Handle(request, Token);

        Assert.True(first.IsSuccess && retry.IsSuccess);
        Assert.Equal(first.Value.Id, retry.Value.Id);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Text_with_a_secret_shaped_string_is_scrubbed_before_the_event_is_written()
    {
        await _h.Lease();
        using var cli = _h.Cli();
        var token = "ghp_" + new string('a', 36);

        var added = await _h.Add().Handle(new AddTodoRequest($"push with {token}"), Token);

        Assert.True(added.IsSuccess, added.IsError ? added.Error.ToString() : null);
        Assert.DoesNotContain(token, _h.Payload<TodoAdded>(Assert.Single(_h.Slice.Store.Events)).Text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "SND-TODO-001")]
    [InlineData("pay rent 📅 2026-12-01", "SND-TODO-007")]
    [InlineData("two\nlines", "SND-TODO-007")]
    [InlineData("zero\u200Bwidth", "SND-TODO-007")]
    public async Task Bad_text_is_refused_and_nothing_is_recorded(string text, string code)
    {
        await _h.Lease();
        using var cli = _h.Cli();

        var refused = await _h.Add().Handle(new AddTodoRequest(text), Token);

        Assert.True(refused.IsError);
        Assert.Equal(code, refused.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_link_of_no_known_shape_or_an_11th_link_is_SND_TODO_004()
    {
        await _h.Lease();
        using var cli = _h.Cli();

        var odd = await _h.Add().Handle(new AddTodoRequest("x", Links: ["nonsense"]), Token);
        var many = await _h.Add().Handle(new AddTodoRequest("x", Links: [.. Enumerable.Range(1, TodoLinks.Max + 1).Select(n => $"ENG-{TodoHarness.Num(n)}")]), Token);

        Assert.Equal(["SND-TODO-004", "SND-TODO-004"], [odd.Error.Code, many.Error.Code]);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_501st_open_todo_is_SND_TODO_006()
    {
        await _h.Lease();
        for (var n = 0; n < 500; n++)
        {
            await _h.Seed(TodoId.New(_h.Slice.Ids), text: $"todo {TodoHarness.Num(n)}");
        }

        using var cli = _h.Cli();
        var over = await _h.Add().Handle(new AddTodoRequest("one too many"), Token);

        Assert.True(over.IsError);
        Assert.Equal("SND-TODO-006", over.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_may_add_20_todos_an_hour_and_the_21st_is_SND_TODO_429_with_the_agent_text()
    {
        await _h.Lease();
        using var session = _h.Session();

        for (var n = 0; n < 20; n++)
        {
            var added = await _h.Add().Handle(new AddTodoRequest($"todo {TodoHarness.Num(n)}", ClientId: TodoClientId.From($"c-{TodoHarness.Num(n)}")), Token);
            Assert.True(added.IsSuccess, added.IsError ? added.Error.ToString() : null);
        }

        var retried = await _h.Add().Handle(new AddTodoRequest("todo 19", ClientId: TodoClientId.From("c-19")), Token);   // a retry doesn't count again
        var twentyFirst = await _h.Add().Handle(new AddTodoRequest("one more", ClientId: TodoClientId.From("c-20")), Token);

        Assert.True(retried.IsSuccess, retried.IsError ? retried.Error.ToString() : null);
        Assert.True(twentyFirst.IsError);
        Assert.Equal("SND-TODO-429", twentyFirst.Error.Code);
        Assert.Contains("An agent can add 20 todos an hour. Try again after", twentyFirst.Error.Message, StringComparison.Ordinal);
        Assert.Equal(20, _h.Slice.Store.Events.Count);

        _h.Slice.Clock.Advance(TimeSpan.FromMinutes(61));
        var later = await _h.Add().Handle(new AddTodoRequest("one more", ClientId: TodoClientId.From("c-20")), Token);
        Assert.True(later.IsSuccess, later.IsError ? later.Error.ToString() : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_owners_terminal_never_counts_against_the_hourly_limit()
    {
        await _h.Lease();
        using var terminal = _h.Slice.Callers.Enter(A.Caller().Human().AtTerminal().Client(ClientLabel.Cli).Build());

        for (var n = 0; n < 25; n++)
        {
            var added = await _h.Add().Handle(new AddTodoRequest($"todo {TodoHarness.Num(n)}", ClientId: TodoClientId.From($"c-{TodoHarness.Num(n)}")), Token);
            Assert.True(added.IsSuccess, added.IsError ? added.Error.ToString() : null);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sindri_launched_session_is_admitted_and_its_actor_is_recorded()
    {
        await _h.Lease();
        using var launched = _h.Launched();

        var added = await _h.Add().Handle(new AddTodoRequest("found while fixing the sync test"), Token);

        Assert.True(added.IsSuccess, added.IsError ? added.Error.ToString() : null);
        var payload = _h.Payload<TodoAdded>(Assert.Single(_h.Slice.Store.Events));
        Assert.StartsWith("session:", payload.By, StringComparison.Ordinal);
    }
}
