using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>
/// The note sync (todo spec §4.5): one pass at a time, run by the watch host, and the first import. Passes and imports share one gate. A failure is in <see cref="Status"/>, never thrown; the status lives in memory
/// (reset by a restart). Each pass logs one JSON line inside a 'todo.note.pass' span: debug for a pass that changed nothing, information for one that did, warning for a failure. Never a todo's text.
/// </summary>
/// <param name="deps">What a pass reaches.</param>
internal sealed partial class TodoNoteSync(SyncDeps deps) : ITodoNoteSync, IDisposable
{
    private static readonly ActivitySource _source = new("Aw.Sindri.Todo");

    private readonly SemaphoreSlim _gate = new(1, 1);
    private TodoNoteStatus _status = TodoNoteStatus.NotSet;

    public void Dispose() => _gate.Dispose();

    public TodoNoteStatus Status => Volatile.Read(ref _status);

    public async ValueTask<TodoNoteStatus> Pass(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var located = await deps.Locator.Locate(cancellationToken).ConfigureAwait(false);
            if (located.IsError)
            {
                Fail(located.Error.Message);
            }
            else if (located.Value is not { } place)
            {
                try
                {
                    deps.Files.DeleteBases(keep: null);
                    Volatile.Write(ref _status, TodoNoteStatus.NotSet);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Fail(e.Message);
                }
            }
            else
            {
                _ = await Run(place, PassMode.Normal, cancellationToken).ConfigureAwait(false);
            }

            return Status;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async ValueTask<Result<TodoNoteImport>> Import(TodoNoteImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var placed = await PlaceOf(request, cancellationToken).ConfigureAwait(false);
            if (placed.IsError)
            {
                return placed.Error;
            }

            var counts = NoteCounter.Count(NoteParser.Parse(deps.Files.Read(placed.Value.Path).Text ?? string.Empty));
            if (counts.Importable == 0)
            {
                return SindriErrors.TodoNothingToImport.With(SindriErrors.TodoNothingToImport.Title);
            }

            if (request.ExpectOpen is not { } expected)
            {
                return new TodoNoteImport(counts, Imported: 0, DryRun: true);
            }

            return expected != counts.Importable
                ? SindriErrors.TodoConflict.With(string.Create(CultureInfo.InvariantCulture, $"The note has {counts.Importable} lines to import, not {expected}. Check the note again."))
                : await Adopt(request, placed.Value, counts, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask<Result<NotePlace>> PlaceOf(TodoNoteImportRequest request, CancellationToken cancellationToken)
    {
        var located = await deps.Locator.Locate(cancellationToken).ConfigureAwait(false);
        if (located.IsError)
        {
            return located.Error;
        }

        return located.Value is { } place && string.Equals(place.Vault, request.Vault, StringComparison.Ordinal) && string.Equals(place.Note, request.Note, StringComparison.Ordinal)
            ? place
            : SindriErrors.TodoNoteKey.With("todo.obsidian: the profile doesn't name that vault and note (fix: sindri todo note set <vault> <note>)");
    }

    private async ValueTask<Result<TodoNoteImport>> Adopt(TodoNoteImportRequest request, NotePlace place, NoteCounts counts, CancellationToken cancellationToken)
    {
        var ran = await Run(place, PassMode.Import, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        if (ran.Value.State != TodoNoteState.Syncing || ran.Value.Error is not null)
        {
            return SindriErrors.TodoConflict.With(ran.Value.Error ?? $"The note can't be imported now ({ran.Value.State}). Try again.");
        }

        if (ran.Value.Adopted != counts.Importable)
        {
            // why: the count was read without the quiet wait, the adopting pass reads after it; the note grew or shrank in between
            return SindriErrors.TodoConflict.With(string.Create(CultureInfo.InvariantCulture, $"The note had {counts.Importable} lines to import when you checked and {ran.Value.Adopted} when they were taken. Check the note again."));
        }

        var events = new NoteEvents(deps, new Dictionary<Core.Ids.TodoId, long>(), string.Empty);
        var recorded = await events.Imported(new TodoNoteImported(place.Vault, place.Note, ran.Value.Adopted, request.By, request.Via), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new TodoNoteImport(counts, ran.Value.Adopted, DryRun: false);
    }

    private async ValueTask<Result<SyncOutcome>> Run(NotePlace place, PassMode mode, CancellationToken cancellationToken)
    {
        using var span = _source.StartActivity("todo.note.pass");
        var started = deps.Clock.GetTimestamp();
        var ran = await Attempt(place, mode, cancellationToken).ConfigureAwait(false);
        var outcome = ran.IsError ? new SyncOutcome(Status.State == TodoNoteState.NotSet ? TodoNoteState.Syncing : Status.State, ran.Error.Message) : ran.Value;
        var line = outcome.Line(place, (long)deps.Clock.GetElapsedTime(started).TotalMilliseconds);
        _ = span?.SetTag("outcome", outcome.State.ToString());
        Record(outcome);
        if (outcome.Error is not null)
        {
            LogFailed(deps.Logger, line);
        }
        else if (outcome.Events > 0 || outcome.Written > 0)
        {
            LogChanged(deps.Logger, line);
        }
        else
        {
            LogUnchanged(deps.Logger, line);
        }

        return ran.IsError ? ran.Error : outcome;
    }

    // a disk error (a full disk, a folder that lost its permissions) is a failed pass in the status, not an exception out of the watch's call
    private async ValueTask<Result<SyncOutcome>> Attempt(NotePlace place, PassMode mode, CancellationToken cancellationToken)
    {
        try
        {
            return await new SyncPass(deps, place, mode).Run(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new SyncOutcome(Status.State == TodoNoteState.NotSet ? TodoNoteState.Syncing : Status.State, e.Message);
        }
    }

    private void Record(SyncOutcome outcome)
    {
        var before = Status;
        var failed = outcome.Error is not null;
        Volatile.Write(ref _status, new TodoNoteStatus(outcome.State, deps.Clock.GetUtcNow(), outcome.Error, failed ? before.FailedInARow + 1 : 0, outcome.NotAdopted));
    }

    private void Fail(string error)
    {
        var before = Status;
        Volatile.Write(ref _status, before with { State = before.State == TodoNoteState.NotSet ? TodoNoteState.Syncing : before.State, LastCheckAt = deps.Clock.GetUtcNow(), LastError = error, FailedInARow = before.FailedInARow + 1 });
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "todo.note.pass {Line}")]
    private static partial void LogUnchanged(ILogger logger, string line);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "todo.note.pass {Line}")]
    private static partial void LogChanged(ILogger logger, string line);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "todo.note.pass {Line}")]
    private static partial void LogFailed(ILogger logger, string line);
}
