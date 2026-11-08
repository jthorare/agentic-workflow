using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ChangeTodo;

/// <summary>Edit a todo (POST /sindri/todos/{id}/change, 'sindri todo edit'; todo spec §4.4): request class, needs the todo's version, appends 'todo.changed'.</summary>
/// <param name="Id">The todo.</param>
/// <param name="Version">The version the caller saw; a stale one is SND-TODO-409.</param>
/// <param name="Text">New text, or null to leave it.</param>
/// <param name="Due">A new due date, or null to leave it.</param>
/// <param name="ClearDue">Whether to drop the due date.</param>
/// <param name="Links">The new whole list of typed links (empty clears them), or null to leave them.</param>
public sealed record ChangeTodoRequest(TodoId Id, long Version, string? Text = null, DateOnly? Due = null, bool ClearDue = false, IReadOnlyList<string>? Links = null);
