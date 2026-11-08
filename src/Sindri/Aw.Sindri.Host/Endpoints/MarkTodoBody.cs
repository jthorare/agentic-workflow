using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.MarkTodo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos/{id}/done, /reopen, /remove and /restore: the version the caller saw, or none for the current one.</summary>
/// <param name="Version">The version the caller saw, or null.</param>
internal sealed record MarkTodoBody(long? Version = null)
{
    /// <summary>The request.</summary>
    /// <param name="id">The todo from the route.</param>
    /// <param name="verb">What to do.</param>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request.</returns>
    internal MarkTodoRequest ToRequest(TodoId id, TodoVerb verb, bool piped) => new(id, verb, Version, piped);
}
