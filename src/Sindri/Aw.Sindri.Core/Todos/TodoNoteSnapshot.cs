using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Core.Todos;

/// <summary>The Obsidian todo note as it is on disk right now (todo spec §4.4: a pick re-reads the note before it acts).</summary>
/// <param name="Hash">The sha256 of the note's bytes ('sha256:...').</param>
/// <param name="Document">The parsed note.</param>
public sealed record TodoNoteSnapshot(string Hash, NoteDocument Document);
