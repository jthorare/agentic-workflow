
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The note ticked the todo.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="On">The line's done date, or null.</param>
public sealed record NoteDone(TodoId Id, DateOnly? On) : NoteIntent(Id);
