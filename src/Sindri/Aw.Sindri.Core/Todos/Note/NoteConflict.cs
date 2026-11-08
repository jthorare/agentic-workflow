
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Both sides changed the todo, differently (D11). Sindri keeps its version, the note's line is left as it is.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Sindri">Sindri's version.</param>
/// <param name="Obsidian">The note's version.</param>
public sealed record NoteConflict(TodoId Id, NoteSide Sindri, NoteSide Obsidian) : NoteIntent(Id);
