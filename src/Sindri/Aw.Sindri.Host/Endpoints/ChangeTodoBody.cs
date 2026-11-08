using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ChangeTodo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos/{id}/change: the version the caller saw and what to change. A field left out (or null) stays as it was; "" in due drops the date and [] in links drops the links.</summary>
/// <param name="Version">The version the caller saw (required).</param>
/// <param name="Text">New text, or null.</param>
/// <param name="Due">A new due date as 'yyyy-MM-dd', "" to clear it, or null to leave it.</param>
/// <param name="Links">The new whole list of links, or null.</param>
internal sealed record ChangeTodoBody(long? Version = null, string? Text = null, string? Due = null, IReadOnlyList<string>? Links = null)
{
    /// <summary>The request for <paramref name="id"/>.</summary>
    /// <param name="id">The todo from the route.</param>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request, SND-TODO-001 for no version, or SND-TODO-003 for a bad date.</returns>
    internal Result<ChangeTodoRequest> ToRequest(TodoId id, bool piped)
    {
        if (Version is not { } version)
        {
            return SindriErrors.TodoText.With("change needs a version and at least one of text, due, links.");
        }

        var due = TodoWire.Due(Due);
        return due.IsError ? due.Error : new ChangeTodoRequest(id, version, Text, due.Value.Date, due.Value.Clear, Links, piped);
    }
}
