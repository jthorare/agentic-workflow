namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>The open both-places conflict of a todo (todo spec §3.1).</summary>
/// <param name="Sindri">Sindri's side.</param>
/// <param name="Obsidian">The note's side.</param>
/// <param name="NoteHash">The note the conflict was found against.</param>
internal sealed record TodoConflictView(TodoSideView Sindri, TodoSideView Obsidian, string NoteHash);
