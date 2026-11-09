using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.note.held.resolved' payload (todo spec §4.1): the owner (or an agent) picked what happens to the held changes, or a pass found too few of them to hold.</summary>
/// <param name="Kept">'applied', 'restored' or 'dissolved'.</param>
/// <param name="NoteHash">The note hash of the hold that was resolved.</param>
/// <param name="Count">How many held changes the pick covered.</param>
/// <param name="By">Who picked.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoNoteHeldResolved(string Kept, string NoteHash, int Count, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
