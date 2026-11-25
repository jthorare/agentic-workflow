using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Core.Todos;

/// <summary>The hold the ledger says is open (todo spec §4.5 "The hold").</summary>
/// <param name="Changes">The held changes.</param>
/// <param name="NoteHash">The note hash the hold was drawn from.</param>
/// <param name="Sequence">The ledger sequence of the 'todo.note.held' (or '.held.reopened') event that opened it: a todo row written after it was changed after the hold was drawn.</param>
/// <param name="OpenInBase">The open-todo count of the base the hold was drawn against, from the event; null on an event written before the field existed.</param>
public sealed record TodoOpenHold(IReadOnlyList<HeldChange> Changes, string NoteHash, long Sequence, int? OpenInBase = null);
