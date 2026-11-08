using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.UndoTodoHold;

/// <summary>
/// Undoes a hold pick inside its window (todo spec §4.1, REQ-E7; request class): SND-TODO-005 when there is no pick to undo or its 10 s are over. Reverses what Apply changed with ordinary state events (a todo that moved
/// since is left as it is), then appends 'todo.note.held.reopened'; the hold is open again with its stored changes.
/// </summary>
/// <param name="writes">The caller, the todos' rows and the appends.</param>
/// <param name="converger">Appends the reverse events.</param>
/// <param name="window">Holds the pick.</param>
internal sealed class UndoTodoHoldService(TodoPickWrites writes, TodoConverger converger, TodoPickWindow window)
    : IService<UndoTodoHoldRequest, UndoTodoHoldResponse>
{
    public async ValueTask<Result<UndoTodoHoldResponse>> Handle(UndoTodoHoldRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var who = writes.Begin("UndoTodoHold", request.Piped);
        if (who.IsError)
        {
            return who.Error;
        }

        if (window.FindHold(who.Value.Now) is not { } pick)
        {
            return SindriErrors.TodoNothingToPick.With("There's nothing to undo.");
        }

        var via = who.Value.Via;
        foreach (var (prior, versionAfter) in pick.Priors)
        {
            var back = await Reverse(prior, versionAfter, who.Value, cancellationToken).ConfigureAwait(false);
            if (back is not null)
            {
                return back;
            }
        }

        var appended = await writes.Append(who.Value, EventTypes.TodoNoteHeldReopened, new TodoNoteHeldReopened(who.Value.By, via), writes.Key("todo.note.held.reopened"), cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        window.ForgetHold();
        return new UndoTodoHoldResponse(Kept: null);
    }

    // a todo that moved since Apply, or is gone, is left as it is
    private async ValueTask<Error?> Reverse(TodoRow prior, long versionAfter, TodoPickCaller who, CancellationToken cancellationToken)
    {
        var read = await writes.Row(prior.Id, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value.Version != versionAfter)
        {
            return null;
        }

        var back = await converger.Apply(read.Value, TodoNoteSides.Of(prior), who.By, who, cancellationToken).ConfigureAwait(false);
        return back.IsError ? back.Error : null;
    }
}
