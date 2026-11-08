using System.Text.Json.Serialization;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.conflict.resolved' payload (todo spec §4.1, D11): a caller picked a side of an open conflict.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Kept">The side kept.</param>
/// <param name="BaseLine">The note line the pick was made against; it becomes that id's base line.</param>
/// <param name="By">Who picked.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoConflictResolved(TodoId Id, TodoSideName Kept, string BaseLine, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
