namespace Aw.Sindri.Ui.Api;

/// <summary>The body of the note check and of the note set (todo spec §4.4): which vault and which note in it. Guessed shape: the spec names the fields, no golden exists yet.</summary>
/// <param name="Vault">The name of one of the library's vaults.</param>
/// <param name="Note">The note's vault-relative path, such as Todo.md.</param>
internal sealed record TodoNoteTarget(string Vault, string Note);
