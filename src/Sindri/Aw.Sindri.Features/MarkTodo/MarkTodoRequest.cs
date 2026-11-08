using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.MarkTodo;

/// <summary>Check off, reopen, remove or restore a todo (POST /sindri/todos/{id}/{verb}; todo spec §4.4): request class. Without a version the current one is used (last writer wins, 'by' recorded).</summary>
/// <param name="Id">The todo.</param>
/// <param name="Verb">What to do.</param>
/// <param name="Version">The version the caller saw, or null; a stale one is SND-TODO-409.</param>
public sealed record MarkTodoRequest(TodoId Id, TodoVerb Verb, long? Version = null);
