
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The note changed fields of a todo; only the changed ones are set.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Text">The new scrubbed text, or null when unchanged.</param>
/// <param name="DueChanged">True when the due date changed (to <paramref name="Due"/>, null clears it).</param>
/// <param name="Due">The new due date.</param>
/// <param name="Links">The new links, or null when unchanged ([] clears them).</param>
public sealed record NoteChange(TodoId Id, string? Text, bool DueChanged, DateOnly? Due, IReadOnlyList<string>? Links) : NoteIntent(Id);
