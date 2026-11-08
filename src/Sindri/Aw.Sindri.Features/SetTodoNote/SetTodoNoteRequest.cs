namespace Aw.Sindri.Features.SetTodoNote;

/// <summary>Set both fields of 'todo.obsidian' in the live profile ('sindri todo note set', todo spec §4.4, §4.5): one validated write, live at once.</summary>
/// <param name="Vault">The name of one of the library's vaults.</param>
/// <param name="Note">The note's path inside it, such as `Todo.md`.</param>
public sealed record SetTodoNoteRequest(string Vault, string Note);
