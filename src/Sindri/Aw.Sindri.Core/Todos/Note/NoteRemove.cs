
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The note's line is gone: the todo is removed in Sindri.</summary>
/// <param name="Id">The todo's id.</param>
public sealed record NoteRemove(TodoId Id) : NoteIntent(Id);
