using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.note.held' payload (todo spec §4.1, REQ-E7): a sync pass found more note changes than it applies on its own and holds them for the owner.</summary>
/// <param name="Changes">The held changes.</param>
/// <param name="NoteHash">The sha256 of the note the hold was drawn from; Apply and Put them back send it back.</param>
/// <param name="By">Who wrote the event (the note service).</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoNoteHeld(IReadOnlyList<TodoHeldChange> Changes, string NoteHash, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
