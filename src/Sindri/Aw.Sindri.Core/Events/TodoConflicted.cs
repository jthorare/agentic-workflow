
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.conflicted' payload (todo spec §4.1, D11): a sync found one todo changed in Sindri and in the note since the last sync, and the two differ.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Sindri">Sindri's version.</param>
/// <param name="Obsidian">The note's version.</param>
/// <param name="NoteHash">The sha256 of the note the conflict was found in.</param>
public sealed record TodoConflicted(TodoId Id, TodoSide Sindri, TodoSide Obsidian, string NoteHash);
