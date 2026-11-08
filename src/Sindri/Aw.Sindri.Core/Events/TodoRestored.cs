using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.restored' payload (todo spec §4.1): a removed todo came back (the Undo of a delete, or a line back in the note).</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Version">Its version after the change.</param>
/// <param name="By">Who wrote it.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoRestored(TodoId Id, long Version, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
