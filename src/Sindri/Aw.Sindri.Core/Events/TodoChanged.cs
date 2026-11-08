using System.Text.Json.Serialization;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'todo.changed' payload (todo spec §4.1): a todo was edited. A field that is null is left as it was; <see cref="Links"/>, when set, replaces the whole list
/// (an empty list clears it); a due date the writer emptied is named by <see cref="ClearDue"/>, so "unchanged" and "cleared" never share a value.
/// </summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Version">Its version after the change.</param>
/// <param name="Text">New text (scrubbed), or null.</param>
/// <param name="Due">A new due date, or null.</param>
/// <param name="ClearDue">Whether the due date is dropped.</param>
/// <param name="Links">The new whole list of links, or null.</param>
/// <param name="By">Who wrote it.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoChanged(TodoId Id, long Version, string? Text, DateOnly? Due, bool ClearDue, IReadOnlyList<string>? Links, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
