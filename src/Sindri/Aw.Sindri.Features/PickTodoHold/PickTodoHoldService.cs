using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Text;
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
/// <param name="scrubber">Scrubs the re-read lines as the sync scrubbed the held ones.</param>
internal sealed class PickTodoHoldService(TodoHolds holds, TodoPickWrites writes, TodoConverger converger, ITodoNoteSource note, TodoPickWindow window, ITextScrubber scrubber)
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
        var still = seen.Value is { } snapshot ? new NoteHold(hold.Changes).StillTrue(snapshot.Document, scrubber).Count : hold.Changes.Count;
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

        // why: "put them back" after a failed Apply must leave nothing of that try behind, so the todos it changed go back to before it
        if (request.Restore && await Revert(priors, who, cancellationToken).ConfigureAwait(false) is { } stuck)
        {
            return stuck;
        }

        var kept = request.Restore ? "restored" : "applied";
        var resolved = new TodoNoteHeldResolved(kept, hold.NoteHash, hold.Changes.Count, who.By, via);
        var appended = await writes.Append(who, EventTypes.TodoNoteHeldResolved, resolved, writes.Key($"todo.note.held.resolved:{hold.NoteHash}"), cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        window.ForgetPartial();
        window.RememberHold(new TodoHoldPick(who.Now, kept, request.Restore ? [] : priors, who.By));
        return new PickTodoHoldResponse(kept, hold.Changes.Count, TodoPickWindow.Until(who.Now));
    }

    private async ValueTask<Error?> Revert(List<(TodoRow Prior, long VersionAfter)> priors, TodoPickCaller who, CancellationToken cancellationToken)
    {
        foreach (var (prior, versionAfter) in priors)
        {
            if (await writes.Revert(converger, prior, versionAfter, who, cancellationToken).ConfigureAwait(false) is { } back)
            {
                return back;
            }
        }

        return null;
    }

    private async ValueTask<Error?> Apply(TodoOpenHold hold, TodoPickCaller who, List<(TodoRow Prior, long VersionAfter)> priors, CancellationToken cancellationToken)
    {
        foreach (var change in hold.Changes)
        {
            // a todo changed by the try that failed is already at the held version
            var failed = priors.Exists(p => p.Prior.Id == change.Id) ? null : await ApplyOne(change, hold, who, priors, cancellationToken).ConfigureAwait(false);
            if (failed is not null)
            {
                window.RememberPartial(hold.NoteHash, priors);
                return failed;
            }
        }

        return null;
    }

    private async ValueTask<Error?> ApplyOne(HeldChange change, TodoOpenHold hold, TodoPickCaller who, List<(TodoRow Prior, long VersionAfter)> priors, CancellationToken cancellationToken)
    {
        var read = await writes.Row(change.Id, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            // a held change for a todo that is gone has nothing to apply
            return null;
        }

        var row = read.Value;
        var target = change.Kind == HeldKind.Removed || change.NoteLine is null
            ? new TodoSide(Text: null, Due: null, Links: [], Done: false, Removed: true)
            : TodoNoteSides.FromLine(change.NoteLine, row.Links);
        if (row.Sequence > hold.Sequence && !TodoNoteSides.Same(target, TodoNoteSides.Of(row)))
        {
            // D11, no silent winner: the todo was written after the hold was drawn, so the note's older line is not applied over it; the two sides go to the todo's conflict
            return await Conflict(row, target, hold, who, cancellationToken).ConfigureAwait(false);
        }

        var version = await converger.Apply(row, target, "obsidian", who, cancellationToken).ConfigureAwait(false);
        if (version.IsError)
        {
            return version.Error;
        }

        priors.Add((row, version.Value));
        return null;
    }

    // a todo that already has a conflict open keeps it
    private async ValueTask<Error?> Conflict(TodoRow row, TodoSide target, TodoOpenHold hold, TodoPickCaller who, CancellationToken cancellationToken)
    {
        if (row.Conflict is not null)
        {
            return null;
        }

        var key = $"todo.conflicted:{row.Id.Value}:{hold.NoteHash}";
        var appended = await writes.Append(who, EventTypes.TodoConflicted, new TodoConflicted(row.Id, TodoNoteSides.Of(row), target, hold.NoteHash), key, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : null;
    }
}
