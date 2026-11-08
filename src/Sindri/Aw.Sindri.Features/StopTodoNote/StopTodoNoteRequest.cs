using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.TodoNoteProfile;

namespace Aw.Sindri.Features.StopTodoNote;

/// <summary>Remove 'todo.obsidian' from the live profile (POST /sindri/todos/note/stop, the Settings card's Stop syncing; todo spec §4.4, §4.5 "Stopping"): the sync service stops and the note stays as it is. Request class.</summary>
/// <param name="IfHash">The live profile's hash the caller saw; another hash is SND-TODO-409 and nothing is written. Null: write whatever is live.</param>
public sealed record StopTodoNoteRequest(string? IfHash = null) : ICommand<TodoNoteWritten>;
