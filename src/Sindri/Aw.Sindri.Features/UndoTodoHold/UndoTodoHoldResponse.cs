namespace Aw.Sindri.Features.UndoTodoHold;

/// <summary>What a hold Undo answers: nothing is kept any more, so 'kept' is null and the hold is open again.</summary>
/// <param name="Kept">Always null.</param>
public sealed record UndoTodoHoldResponse(string? Kept);
