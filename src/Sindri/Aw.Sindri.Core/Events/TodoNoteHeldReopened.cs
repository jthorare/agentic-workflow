using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.note.held.reopened' payload (todo spec §4.1): a hold pick was undone inside its window, so the hold is open again with its stored changes.</summary>
/// <param name="By">Who undid the pick.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoNoteHeldReopened(string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
