namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The note's top-level task lines as Sindri last synced them, scrubbed (todo spec §4.5 "The base"). A missing base is a null <see cref="NoteBase"/>, not an empty one.</summary>
/// <param name="Lines">The task lines, without line endings.</param>
public sealed record NoteBase(IReadOnlyList<string> Lines);
