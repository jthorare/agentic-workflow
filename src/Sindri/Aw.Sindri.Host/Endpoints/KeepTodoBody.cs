using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.KeepTodo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos/{id}/keep: the side to keep, 'sindri' or 'obsidian'.</summary>
/// <param name="Side">The side.</param>
internal sealed record KeepTodoBody(string? Side = null)
{
    /// <summary>The request.</summary>
    /// <param name="id">The todo from the route.</param>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request, or SND-CLI-003 for a side that is neither.</returns>
    internal Result<KeepTodoRequest> ToRequest(TodoId id, bool piped) => Side switch
    {
        "sindri" => new KeepTodoRequest(id, TodoSideName.Sindri, piped),
        "obsidian" => new KeepTodoRequest(id, TodoSideName.Obsidian, piped),
        _ => SindriErrors.InvalidInput.With("side is sindri or obsidian (fix: {\"side\":\"sindri\"})"),
    };
}
