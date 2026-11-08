
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>One change the note made that a hold is holding.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Kind">The kind.</param>
/// <param name="NoteLine">The note's line for it ('todo.note.held' carries it); null for Removed.</param>
public sealed record HeldChange(TodoId Id, HeldKind Kind, string? NoteLine);
