namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What the disk says about the note's own file (todo spec §4.5 "Setting it").</summary>
/// <param name="Exists">Whether the file is there.</param>
/// <param name="IsSymlink">Whether the path, or a folder on the way to it inside the vault, is a symlink: Sindri never follows one.</param>
/// <param name="LinkCount">The file's hard links; more than one is refused.</param>
/// <param name="Text">The file's text, or null when it doesn't exist or can't be read.</param>
/// <param name="Unreadable">Why the file couldn't be read (too big, no permission), or null.</param>
public sealed record NoteFileFacts(bool Exists, bool IsSymlink, int LinkCount, string? Text, string? Unreadable);
