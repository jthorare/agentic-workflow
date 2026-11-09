namespace Aw.Sindri.Host.Commands;

/// <summary>The note the live profile's 'todo.obsidian' names, for 'sindri todo note import'.</summary>
/// <param name="Vault">The vault name.</param>
/// <param name="Note">The note path inside it.</param>
internal sealed record TodoNoteTarget(string Vault, string Note);
