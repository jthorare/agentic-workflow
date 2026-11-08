
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>A removed todo's line came back as the owner's: the todo is restored (a NoteChange follows when the line also differs).</summary>
/// <param name="Id">The todo's id.</param>
public sealed record NoteRestore(TodoId Id) : NoteIntent(Id);
