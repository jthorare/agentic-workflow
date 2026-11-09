using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.KeepTodoUndo;

/// <summary>Undo a pick inside its 10 seconds (POST /sindri/todos/{id}/keep/undo; todo spec D11, §4.4): request class.</summary>
/// <param name="Id">The todo.</param>
/// <param name="Piped">Whether the request came with fence=1: the event records 'via: pipe'.</param>
public sealed record KeepTodoUndoRequest(TodoId Id, bool Piped = false) : ICommand<KeepTodoUndoResponse>;
