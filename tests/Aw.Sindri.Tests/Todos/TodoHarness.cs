using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.GetTodoNote;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Features.MarkTodo;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Todos;

/// <summary>
/// The todo services over an in-memory ledger and views. The services are stubs with no constructor parameters (the slice recipe's red step): when they gain their ports,
/// the factories below pass them, and no test changes.
/// </summary>
internal sealed class TodoHarness
{
    internal static readonly TodoId First = TodoId.From("td_01jc5a8xq0n4w7ms2t9r3vbk6e");
    internal static readonly TodoId Second = TodoId.From("td_01jc5b2kz8m3r6tq1w9xh4dn7y");

    /// <summary>A number as text in the invariant culture.</summary>
    internal static string Num(long n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal ViewsHarness Views { get; } = new();

    internal SliceHarness Slice => Views.Slice;

#pragma warning disable CA1822 // why: the factories take their ports from this harness once the services have them (the slice recipe's red step)
    internal AddTodoService Add() => new();

    internal ChangeTodoService Change() => new();

    internal MarkTodoService Mark() => new();

    internal ListTodosService List() => new();

    internal GetTodoService Get() => new();

    internal GetTodoNoteService Note() => new();
#pragma warning restore CA1822

    internal async Task Lease() => _ = await Slice.HoldLease();

    internal IDisposable Cli() => Slice.Callers.Enter(A.Caller().Human().Client(ClientLabel.Cli).Build());

    internal IDisposable Session() => Slice.Callers.Enter(A.Caller().Session().Build());

    internal IDisposable Launched() => Slice.Callers.Enter(A.Caller().Launched().Build());

    /// <summary>Puts an open todo at version 1 in the view, added by <paramref name="by"/>.</summary>
    internal Task Seed(TodoId id, string text = "reply to Sam", string by = "human:ui", DateOnly? due = null, string? via = null) =>
        Views.Fold(EventTypes.TodoAdded, new TodoAdded(id, text, due, Links: [], ClientId: null, by, via));

    internal Task Fold<TPayload>(string type, TPayload payload) => Views.Fold(type, payload);

    internal async Task<TodoRow?> Row(TodoId id) => (await Views.Views.Get<TodoRow>(ViewNames.Todo, id.Value, TestContext.Current.CancellationToken)).Value;

    internal T Payload<T>(LedgerEvent recorded) => Slice.Events.Payload<T>(recorded).Value;
}
