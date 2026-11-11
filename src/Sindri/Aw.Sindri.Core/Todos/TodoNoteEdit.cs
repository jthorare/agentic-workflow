namespace Aw.Sindri.Core.Todos;

/// <summary>What an edit of 'todo.obsidian' did to the live profile.</summary>
/// <param name="Path">The profile file.</param>
/// <param name="Changed">False when the profile already had that state and the file was left alone.</param>
/// <param name="ProfileHash">The hash of the live profile after the edit (64 lower-case hex digits).</param>
public sealed record TodoNoteEdit(string Path, bool Changed, string ProfileHash);
