using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Features.ReadLedger;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Writes a todo answer with the todos' text inside the untrusted fence (todo spec §4.4 "Fencing"): for a session caller, and for any caller that sends fence=1. A port because the fence lives
/// in the MCP namespace, which already depends on this one (ARCH-CYCLE-001); the Host registers the MCP fence behind it.
/// </summary>
internal interface ITodoFencing
{
    /// <summary>The list as a fenced caller reads it.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The JSON answer with the text fenced.</returns>
    public IResult List(ListTodosResponse list);

    /// <summary>One todo as a fenced caller reads it.</summary>
    /// <param name="todo">The todo.</param>
    /// <returns>The JSON answer with the text fenced.</returns>
    public IResult One(GetTodoResponse todo);

    /// <summary>A ledger page with every string of a 'todo.*' payload but the ids, hashes, enum values, dates, 'by' and 'via' fenced.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The JSON answer with the todo payloads fenced.</returns>
    public IResult Ledger(LedgerPage page);
}
