using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CheckTodoNote;

/// <summary>Look at a note before it is set (POST /sindri/todos/note/check, the Settings card's Check note; todo spec §4.4): request class, writes nothing.</summary>
/// <param name="Vault">The name of one of the library's vaults.</param>
/// <param name="Note">The note's path inside it.</param>
public sealed record CheckTodoNoteRequest(string Vault, string Note) : ICommand<CheckTodoNoteResponse>;
