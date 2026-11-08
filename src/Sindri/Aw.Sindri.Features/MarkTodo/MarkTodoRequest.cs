using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.MarkTodo;

/// <summary>Check off, reopen, remove or restore a todo (POST /sindri/todos/{id}/{verb}; todo spec §4.4): request class. Without a version the current one is used (last writer wins, 'by' recorded).</summary>
/// <param name="Id">The todo.</param>
/// <param name="Verb">What to do.</param>
/// <param name="Version">The version the caller saw, or null; a stale one is SND-TODO-409.</param>
/// <param name="Piped">Whether the request came with fence=1 (the CLI had no terminal on stdout): the event records 'via: pipe'.</param>
public sealed record MarkTodoRequest(TodoId Id, TodoVerb Verb, long? Version = null, bool Piped = false) : ICommand<TodoAccepted>, IClassifiedCommand
{
    /// <inheritdoc/>
    public string? Variant => Verb switch
    {
        TodoVerb.Done => "done",
        TodoVerb.Reopen => "reopen",
        TodoVerb.Remove => "remove",
        TodoVerb.Restore => "restore",
        _ => "unknown",   // why: no catalog row has this variant, so the command is classified protected (fail closed)
    };

    /// <inheritdoc/>
    public bool CarriesProtectedFields => false;
}
