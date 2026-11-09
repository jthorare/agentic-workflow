namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What a swap did (todo spec §4.5 Writing step 2).</summary>
/// <param name="CameOut">The tmp path, which now holds what was at the note's path; null when the note was created or the swap didn't happen.</param>
/// <param name="Problem">Why nothing was written ("can't write to this disk", a tmp file already there, the OS error); null when it was written.</param>
public sealed record NoteSwap(string? CameOut, string? Problem);
