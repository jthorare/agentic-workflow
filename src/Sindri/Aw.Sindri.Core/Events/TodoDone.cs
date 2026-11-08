using System.Text.Json.Serialization;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.done' payload (todo spec §4.1): a todo was checked off (it keeps its row and gets a done time).</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Version">Its version after the change.</param>
/// <param name="By">Who wrote it.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoDone(TodoId Id, long Version, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
