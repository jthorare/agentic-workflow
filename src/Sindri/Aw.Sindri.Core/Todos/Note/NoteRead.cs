namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What one read of the note found (todo spec §4.5 Reading, Limits).</summary>
/// <param name="Text">The note's text; null when <paramref name="Problem"/> is set or the note is missing.</param>
/// <param name="Hash">The sha256 of the note's bytes, lower-case hex; null when there is no text.</param>
/// <param name="Bytes">The note's size in bytes (0 when missing).</param>
/// <param name="Missing">Whether the note doesn't exist (its folder may).</param>
/// <param name="Problem">Why the note can't be read ("over 1 MB", "is a symlink", the OS error); null when it read or is missing.</param>
public sealed record NoteRead(string? Text, string? Hash, long Bytes, bool Missing, string? Problem);
