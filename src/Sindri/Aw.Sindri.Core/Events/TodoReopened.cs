using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.reopened' payload (todo spec §4.1): a done todo was reopened (a reopen, or the Undo of a check-off).</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Version">Its version after the change.</param>
/// <param name="By">Who wrote it.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoReopened(TodoId Id, long Version, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
