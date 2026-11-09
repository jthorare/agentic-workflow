namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>Where the todo note is: the profile's 'todo.obsidian' resolved to the vault's folder (todo spec §4.5 "Setting it").</summary>
/// <param name="Vault">The vault's name.</param>
/// <param name="Note">The note's vault-relative path.</param>
/// <param name="Root">The vault's folder.</param>
internal sealed record NotePlace(string Vault, string Note, string Root)
{
    /// <summary>The note's absolute path.</summary>
    public string Path => System.IO.Path.Combine(Root, Note);
}
