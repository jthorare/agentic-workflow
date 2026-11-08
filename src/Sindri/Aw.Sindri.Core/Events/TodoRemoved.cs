using System.Text.Json.Serialization;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.removed' payload (todo spec §4.1): a todo was deleted (soft: the row stays, marked removed).</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Version">Its version after the change.</param>
/// <param name="By">Who wrote it.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoRemoved(TodoId Id, long Version, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
