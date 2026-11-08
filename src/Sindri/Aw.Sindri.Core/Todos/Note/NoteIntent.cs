
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What the merge wants done in Sindri about one todo; Ph3b maps each onto the events of todo spec §4.1 with 'by: obsidian'.</summary>
/// <param name="Id">The todo's id.</param>
public abstract record NoteIntent(TodoId Id);
