using System.Text.RegularExpressions;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;
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
/// The todo services over an in-memory ledger and views, with a scrubber that masks GitHub tokens.
/// </summary>
#pragma warning disable CA1001 // why: the window's semaphores are never waited on after a test ends, and the test classes that own a harness have nothing to dispose it with
internal sealed partial class TodoHarness
{
    internal static readonly TodoId First = TodoId.From("td_01jc5a8xq0n4w7ms2t9r3vbk6e");
    internal static readonly TodoId Second = TodoId.From("td_01jc5b2kz8m3r6tq1w9xh4dn7y");

    /// <summary>A number as text in the invariant culture.</summary>
    internal static string Num(long n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal ViewsHarness Views { get; } = new();

    internal SliceHarness Slice => Views.Slice;

    private readonly TodoAddWindow _window = new();

    private TodoInput Input() => new(new MaskingScrubber(), Slice.Profiles);

    internal AddTodoService Add() => new(new TodoAdds(Views.Views, Slice.Store, Slice.Events, Slice.Clock, _window), Input(), Slice.Store, Slice.Held, Slice.Events, Slice.Ids, Slice.Callers);

    internal ChangeTodoService Change() => new(Views.Views, Slice.Store, Slice.Held, Slice.Events, Input(), Slice.Callers);

    internal MarkTodoService Mark() => new(Views.Views, Slice.Store, Slice.Held, Slice.Events, Slice.Callers);

    internal ListTodosService List() => new(Views.Views, Views.Progress, Slice.Callers);

    internal GetTodoService Get() => new(Views.Views, Views.Progress, Slice.Callers);

    internal GetTodoNoteService Note() => new(Views.Progress, Slice.Callers);

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

    private sealed partial class MaskingScrubber : ITextScrubber
    {
        public string Scrub(string text) => Token.Replace(text, "[redacted]");

        [GeneratedRegex("ghp_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        private static partial Regex Token { get; }
    }
}
#pragma warning restore CA1001
