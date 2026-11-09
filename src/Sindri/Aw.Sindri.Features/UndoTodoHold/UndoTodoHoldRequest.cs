using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.UndoTodoHold;

/// <summary>Undo a hold pick inside its 10 seconds (POST /sindri/todos/note/hold/undo; todo spec REQ-E7, §4.4): request class.</summary>
/// <param name="Piped">Whether the request came with fence=1: the event records 'via: pipe'.</param>
public sealed record UndoTodoHoldRequest(bool Piped = false) : ICommand<UndoTodoHoldResponse>;
