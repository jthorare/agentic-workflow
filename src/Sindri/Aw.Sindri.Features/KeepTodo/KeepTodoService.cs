using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.KeepTodo;

/// <summary>
/// Picks a side of an open both-places conflict (todo spec §4.1, D11; request class): refuses an unknown todo (SND-TODO-404), a todo with no conflict or a pick still open (SND-TODO-005), and a 'keep obsidian' whose
/// note line moved since the conflict was drawn (SND-TODO-409, checked when the note can be read). Appends 'todo.conflict.resolved', then, for Obsidian's version, the ordinary state events that make the todo match it
/// (by 'obsidian'). Nothing is written to the note: the writer does that when the 10 s Undo window closes.
/// </summary>
/// <param name="writes">The caller, the todo's row and the appends.</param>
/// <param name="converger">Appends what Obsidian's version changes.</param>
/// <param name="note">Re-reads the note.</param>
/// <param name="window">Remembers the pick for its Undo.</param>
internal sealed class KeepTodoService(TodoPickWrites writes, TodoConverger converger, ITodoNoteSource note, TodoPickWindow window)
    : IService<KeepTodoRequest, KeepTodoResponse>
{
    public async ValueTask<Result<KeepTodoResponse>> Handle(KeepTodoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var who = writes.Begin("KeepTodo", request.Piped);
        if (who.IsError)
        {
            return who.Error;
        }

        var read = await writes.Row(request.Id, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var row = read.Value;
        if (row.Conflict is not { } conflict || window.Find(request.Id, who.Value.Now) is not null)
        {
            return SindriErrors.TodoNothingToPick.With("That todo has nothing to sort out.");
        }

        var seen = await Check(request, conflict, cancellationToken).ConfigureAwait(false);
        return seen.IsError ? seen.Error : await Pick(request, who.Value, row, seen.Value, cancellationToken).ConfigureAwait(false);
    }

    // why: with no note service yet (todo Ph3b's sync PR) there is no line to re-read, so a pick checks only the stored conflict
    private async ValueTask<Result<string>> Check(KeepTodoRequest request, TodoOpenConflict conflict, CancellationToken cancellationToken)
    {
        var seen = await note.Read(cancellationToken).ConfigureAwait(false);
        if (seen.IsError)
        {
            return seen.Error;
        }

        if (seen.Value is not { } snapshot)
        {
            return string.Empty;
        }

        return request.Side == TodoSideName.Obsidian && !TodoNoteSides.Same(conflict.Obsidian, TodoNoteSides.Of(snapshot.Document, request.Id))
            ? SindriErrors.TodoConflict.With("The note's line changed since. Reload and try again.")
            : snapshot.Document.Todos.FirstOrDefault(t => request.Id.Equals(t.Id))?.Raw ?? string.Empty;
    }

    private async ValueTask<Result<KeepTodoResponse>> Pick(KeepTodoRequest request, TodoPickCaller who, TodoRow row, string baseLine, CancellationToken cancellationToken)
    {
        var conflict = row.Conflict!;   // why: Handle only calls this for a row with an open conflict
        var via = who.Via;
        var resolved = new TodoConflictResolved(request.Id, request.Side, baseLine, who.By, via);
        var appended = await writes.Append(who, EventTypes.TodoConflictResolved, resolved, writes.Key($"todo.conflict.resolved:{request.Id.Value}"), cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var target = request.Side == TodoSideName.Obsidian ? conflict.Obsidian : TodoNoteSides.Of(row);
        var version = await converger.Apply(row, target, "obsidian", who, cancellationToken).ConfigureAwait(false);
        if (version.IsError)
        {
            return version.Error;
        }

        window.Remember(request.Id, new TodoPick(who.Now, row, version.Value));
        var after = TodoConverger.After(row, target, version.Value, who.Now, "obsidian") with { Conflict = null, PickedConflict = conflict };
        return new KeepTodoResponse(after, TodoPickWindow.Until(who.Now));
    }
}
