using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.SetTodoNote;

/// <summary>Set both fields of 'todo.obsidian' in the live profile (POST /sindri/todos/note/set, the Settings card's Start syncing; todo spec §4.4, §4.5): one validated write, live at once. Request class.</summary>
/// <param name="Vault">The name of one of the library's vaults.</param>
/// <param name="Note">The note's path inside it, such as `Todo.md`.</param>
/// <param name="IfHash">The live profile's hash the caller saw; another hash is SND-TODO-409 and nothing is written. Null: write whatever is live.</param>
public sealed record SetTodoNoteRequest(string Vault, string Note, string? IfHash = null) : ICommand<TodoNoteWritten>;
