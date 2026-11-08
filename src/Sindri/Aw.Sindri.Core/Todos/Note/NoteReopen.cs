
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The note un-ticked the todo.</summary>
/// <param name="Id">The todo's id.</param>
public sealed record NoteReopen(TodoId Id) : NoteIntent(Id);
