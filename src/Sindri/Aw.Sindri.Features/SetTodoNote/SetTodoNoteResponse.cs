namespace Aw.Sindri.Features.SetTodoNote;

/// <summary>What 'todo note set' did.</summary>
/// <param name="Path">The profile file.</param>
/// <param name="Vault">The vault name now in the profile.</param>
/// <param name="Note">The note path now in the profile.</param>
/// <param name="Changed">False when the profile already had both values and the file was left alone.</param>
public sealed record SetTodoNoteResponse(string Path, string Vault, string Note, bool Changed);
