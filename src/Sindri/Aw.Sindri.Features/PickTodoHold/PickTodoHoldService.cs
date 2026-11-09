using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.PickTodoHold;

/// <summary>
/// Applies or puts back the held changes (todo spec §4.1, §4.5 "The hold"; request class): SND-TODO-005 when no hold is open or a hold pick is still in its window, SND-TODO-409 when the caller's note hash isn't the hold's or,
/// when the note can be read, fewer of the held changes are still true in it. 'Apply' appends the ordinary state events (by 'obsidian'), then 'todo.note.held.resolved'; 'Put them back' appends only the resolved event,
/// because the note is rewritten when the 10 s Undo window closes.
/// </summary>
/// <param name="holds">The open hold.</param>
/// <param name="writes">The caller, the todos' rows and the appends.</param>
/// <param name="converger">Appends what Apply changes.</param>
/// <param name="note">Re-reads the note.</param>
/// <param name="window">Remembers the pick for its Undo.</param>
internal sealed class PickTodoHoldService(TodoHolds holds, TodoPickWrites writes, TodoConverger converger, ITodoNoteSource note, TodoPickWindow window)
    : IService<PickTodoHoldRequest, PickTodoHoldResponse>
{
    public async ValueTask<Result<PickTodoHoldResponse>> Handle(PickTodoHoldRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var who = writes.Begin("PickTodoHold", request.Piped);
        if (who.IsError)
        {
            return who.Error;
        }

        var open = await holds.Open(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        if (open.Value is not { } hold || window.FindHold(who.Value.Now) is not null)
        {
            return SindriErrors.TodoNothingToPick.With("The note has no changes waiting.");
        }

        var stale = await Stale(hold, request, cancellationToken).ConfigureAwait(false);
        return stale is not null ? stale : await Pick(request, who.Value, hold, cancellationToken).ConfigureAwait(false);
    }

    // the caller's row must be the hold's, and, when the note can be read, every held change must still be true in it
    private async ValueTask<Error?> Stale(TodoOpenHold hold, PickTodoHoldRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(hold.NoteHash, request.NoteHash, StringComparison.Ordinal))
        {
            return SindriErrors.TodoConflict.With("The note changed again. Check and pick again.");
        }

        var seen = await note.Read(cancellationToken).ConfigureAwait(false);
        if (seen.IsError)
        {
            return seen.Error;
        }

        // why: with no note service yet (todo Ph3b's sync PR) there is nothing to re-read, so the hash the ledger holds is the check
        var still = seen.Value is { } snapshot ? new NoteHold(hold.Changes).StillTrue(snapshot.Document).Count : hold.Changes.Count;
        return still == hold.Changes.Count ? null : SindriErrors.TodoConflict.With($"The note changed again: now {still.ToString(CultureInfo.InvariantCulture)}. Check and pick again.");
    }

    private async ValueTask<Result<PickTodoHoldResponse>> Pick(PickTodoHoldRequest request, TodoPickCaller who, TodoOpenHold hold, CancellationToken cancellationToken)
    {
        var via = who.Via;
        // why: a retry after a failed Apply starts from what the failed try changed, so Undo puts those todos back to before the first try, not to the state the first try left them in
        var priors = new List<(TodoRow Prior, long VersionAfter)>(window.Partial(hold.NoteHash));
        if (!request.Restore && await Apply(hold, who, priors, cancellationToken).ConfigureAwait(false) is { } failed)
        {
            return failed;
        }

        var kept = request.Restore ? "restored" : "applied";
        var resolved = new TodoNoteHeldResolved(kept, hold.NoteHash, hold.Changes.Count, who.By, via);
        var appended = await writes.Append(who, EventTypes.TodoNoteHeldResolved, resolved, writes.Key($"todo.note.held.resolved:{hold.NoteHash}"), cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        window.ForgetPartial();
        window.RememberHold(new TodoHoldPick(who.Now, kept, priors));
        return new PickTodoHoldResponse(kept, hold.Changes.Count, TodoPickWindow.Until(who.Now));
    }

    private async ValueTask<Error?> Apply(TodoOpenHold hold, TodoPickCaller who, List<(TodoRow Prior, long VersionAfter)> priors, CancellationToken cancellationToken)
    {
        foreach (var change in hold.Changes)
        {
            if (priors.Exists(p => p.Prior.Id == change.Id))
            {
                // changed by the try that failed: already at the held version
                continue;
            }

            var read = await writes.Row(change.Id, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                // a held change for a todo that is gone has nothing to apply
                continue;
            }

            var target = change.Kind == HeldKind.Removed || change.NoteLine is null
                ? new TodoSide(Text: null, Due: null, Links: [], Done: false, Removed: true)
                : TodoNoteSides.FromLine(change.NoteLine, read.Value.Links);
            var version = await converger.Apply(read.Value, target, "obsidian", who, cancellationToken).ConfigureAwait(false);
            if (version.IsError)
            {
                window.RememberPartial(hold.NoteHash, priors);
                return version.Error;
            }

            priors.Add((read.Value, version.Value));
        }

        return null;
    }
}
