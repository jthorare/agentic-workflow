namespace Aw.Sindri.Features.StopTodoNote;

/// <summary>What 'todo note stop' did.</summary>
/// <param name="Path">The profile file.</param>
/// <param name="Keys">The key paths that went out of the profile (`todo.obsidian`); empty when the key was already absent.</param>
/// <param name="Changed">False when there was no key to remove and the file was left alone.</param>
public sealed record StopTodoNoteResponse(string Path, IReadOnlyList<string> Keys, bool Changed);
