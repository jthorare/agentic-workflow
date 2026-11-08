namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The two sides of an open conflict now agree (both edited to the same values, or both removed): the conflict closes with no pick (D11).</summary>
/// <param name="Id">The todo's id.</param>
public sealed record NoteConflictSettled(TodoId Id) : NoteIntent(Id);
