using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.KeepTodoUndo;

/// <summary>
/// Undoes a pick inside its window (todo spec §4.1, D11; request class): SND-TODO-404 for an unknown todo, SND-TODO-005 when there is no pick or its 10 s are over, SND-TODO-409 when the todo moved since the pick.
/// Appends the reverse state events of what the pick applied, then 'todo.conflict.reopened'. The note wasn't written in that time, so nothing in the file needs undoing.
/// </summary>
/// <param name="writes">The caller, the todo's row and the appends.</param>
/// <param name="converger">Appends the reverse events.</param>
/// <param name="window">Holds the pick.</param>
internal sealed class KeepTodoUndoService(TodoPickWrites writes, TodoConverger converger, TodoPickWindow window)
    : IService<KeepTodoUndoRequest, KeepTodoUndoResponse>
{
    public async ValueTask<Result<KeepTodoUndoResponse>> Handle(KeepTodoUndoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var who = writes.Begin("KeepTodoUndo", request.Piped);
        if (who.IsError)
        {
            return who.Error;
        }

        var read = await writes.Row(request.Id, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        if (window.Find(request.Id, who.Value.Now) is not { } pick)
        {
            return SindriErrors.TodoNothingToPick.With("There's nothing to undo.");
        }

        return read.Value.Version != pick.VersionAfter
            ? SindriErrors.TodoConflict.With(TodoDecisions.StaleText)
            : await Undo(request, who.Value, read.Value, pick, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<KeepTodoUndoResponse>> Undo(KeepTodoUndoRequest request, TodoPickCaller who, TodoRow row, TodoPick pick, CancellationToken cancellationToken)
    {
        var via = who.Via;
        var back = await converger.Apply(row, new TodoConvergeTarget(TodoNoteSides.Of(pick.Prior), who.By, pick.Prior.DoneAt), who, cancellationToken).ConfigureAwait(false);
        if (back.IsError)
        {
            return back.Error;
        }

        var reopened = new TodoConflictReopened(request.Id, who.By, via);
        var appended = await writes.Append(who, EventTypes.TodoConflictReopened, reopened, writes.Key($"todo.conflict.reopened:{request.Id.Value}"), cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        window.Forget(request.Id);
        return new KeepTodoUndoResponse(pick.Prior with { Version = back.Value, LastBy = who.By, LastVia = via, PickedConflict = null });
    }
}
