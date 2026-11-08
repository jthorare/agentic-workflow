using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Core.Todos;

/// <summary>The hold the ledger says is open (todo spec §4.5 "The hold").</summary>
/// <param name="Changes">The held changes.</param>
/// <param name="NoteHash">The note hash the hold was drawn from.</param>
public sealed record TodoOpenHold(IReadOnlyList<HeldChange> Changes, string NoteHash);
