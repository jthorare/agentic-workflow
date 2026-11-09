namespace Aw.Sindri.Ui.Api;

/// <summary>The body of POST /sindri/todos/note/set: the note, and the profile hash the caller last saw (null the first time).</summary>
/// <param name="Vault">The vault's name.</param>
/// <param name="Note">The note's vault-relative path.</param>
/// <param name="IfHash">The profile hash the caller last got, or null.</param>
internal sealed record TodoNoteSetBody(string Vault, string Note, string? IfHash);
