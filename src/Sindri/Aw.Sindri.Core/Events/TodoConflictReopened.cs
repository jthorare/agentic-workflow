using System.Text.Json.Serialization;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.conflict.reopened' payload (todo spec §4.1, D11): a pick was undone inside its window, so the conflict is open again with its stored versions.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="By">Who undid the pick.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoConflictReopened(TodoId Id, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
