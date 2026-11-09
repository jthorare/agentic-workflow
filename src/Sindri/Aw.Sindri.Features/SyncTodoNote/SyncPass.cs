using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>
/// One run of the note sync (todo spec §4.5): read the base and the note, merge, append the events, write with the swap, save the base and record 'todo.note.synced'. A lost version key (SND-TODO-409) re-runs it from a fresh
/// read, three times at most.
/// </summary>
/// <param name="d">What a pass reaches.</param>
/// <param name="place">The note.</param>
/// <param name="mode">Normal or import.</param>
internal sealed partial class SyncPass(SyncDeps d, NotePlace place, PassMode mode)
{
    /// <summary>What the pass reads once and every step of it shares.</summary>
    private sealed record PassInputs(NoteBase? Base, SyncedMark? Mark, IReadOnlyList<TodoRow> Rows, NoteMergeOptions Options, TodoOpenHold? Hold, bool OneWay)
    {
        public IReadOnlyList<SindriTodo> Sindri { get; init; } = [];
    }

    private const int MaxRuns = 3;
    private const int MaxSwaps = 3;
    private const int AdoptPerPass = 200;
    private static readonly TimeSpan _quiet = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _longest = TimeSpan.FromSeconds(30);

    private readonly SyncLedger _ledger = new(d);
    private readonly PassIdMinter _minter = new(() => TodoId.New(d.Ids));

    /// <summary>Runs the pass.</summary>
    /// <param name="cancellationToken">Stops it between steps.</param>
    /// <returns>What it did, or the error that stopped it.</returns>
    public async ValueTask<Result<SyncOutcome>> Run(CancellationToken cancellationToken)
    {
        for (var run = 1; run < MaxRuns; run++)
        {
            var once = await Once(cancellationToken).ConfigureAwait(false);
            if (!once.IsError || !string.Equals(once.Error.Code, SindriErrors.TodoConflict.Code, StringComparison.Ordinal))
            {
                return once;
            }
        }

        var last = await Once(cancellationToken).ConfigureAwait(false);
        return last.IsError && string.Equals(last.Error.Code, SindriErrors.TodoConflict.Code, StringComparison.Ordinal) ? new SyncOutcome(TodoNoteState.Busy, Error: null) : last;
    }

    private async ValueTask<Result<SyncOutcome>> Once(CancellationToken cancellationToken)
    {
        var inputs = await Inputs(cancellationToken).ConfigureAwait(false);
        if (inputs.IsError)
        {
            return inputs.Error;
        }

        var stopped = await Leftover(inputs.Value, cancellationToken).ConfigureAwait(false);
        if (stopped.IsError)
        {
            return stopped.Error;
        }

        if (stopped.Value is { } stop)
        {
            return stop;
        }

        var read = (await d.Files.ReadQuiet(place.Path, _quiet, _longest, cancellationToken).ConfigureAwait(false)).Read;
        if (Unreadable(read, inputs.Value.Base) is { } why)
        {
            return new SyncOutcome(TodoNoteState.CantRead, why);
        }

        var doc = NoteParser.Parse(read.Text ?? string.Empty);
        var waiting = await Waiting(inputs.Value, doc, cancellationToken).ConfigureAwait(false);
        if (waiting.IsError || waiting.Value)
        {
            return waiting.IsError ? waiting.Error : new SyncOutcome(TodoNoteState.WaitingForImport, Error: null);
        }

        var merged = NoteMerge.Merge(inputs.Value.Base, doc, inputs.Value.Sindri, inputs.Value.Options);
        return await Land(new Merged(inputs.Value, doc, read, merged), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<PassInputs>> Inputs(CancellationToken cancellationToken)
    {
        var synced = await _ledger.LastSynced(cancellationToken).ConfigureAwait(false);
        var rows = await _ledger.Rows(cancellationToken).ConfigureAwait(false);
        var hold = await d.Holds.Open(cancellationToken).ConfigureAwait(false);
        var failed = new Error?[] { synced.IsError ? synced.Error : null, rows.IsError ? rows.Error : null, hold.IsError ? hold.Error : null }.FirstOrDefault(static e => e is not null);
        if (failed is not null)
        {
            return failed;
        }

        var mark = MarkOf(synced.Value);
        var noteBase = mark is null ? null : d.Files.LoadBase(mark.Payload.BaseHash);
        var picked = await Picked(mark, noteBase, cancellationToken).ConfigureAwait(false);
        if (picked.IsError)
        {
            return picked.Error;
        }

        var oneWay = !d.Disk.Probe(place.Root, place.Note).CanSwap;
        var options = Options(rows.Value, hold.Value, picked.Value, oneWay);
        return new PassInputs(noteBase, mark, rows.Value, options, hold.Value, oneWay) { Sindri = [.. rows.Value.Select(r => RowMapping.ToSindri(r, d.Clock))] };
    }

    // a sync of another note is no base: changing the note starts again (todo spec §4.5 First sync and import)
    private SyncedMark? MarkOf(SyncedMark? synced) =>
        synced is not null && string.Equals(synced.Payload.Vault, place.Vault, StringComparison.Ordinal) && string.Equals(synced.Payload.Note, place.Note, StringComparison.Ordinal) ? synced : null;

    private async ValueTask<Result<PickedLines>> Picked(SyncedMark? mark, NoteBase? noteBase, CancellationToken cancellationToken) =>
        mark is null || noteBase is null ? PickedLines.None : await _ledger.Picked(mark.Sequence, cancellationToken).ConfigureAwait(false);

    private NoteMergeOptions Options(IReadOnlyList<TodoRow> rows, TodoOpenHold? hold, PickedLines picked, bool oneWay)
    {
        var (todos, holdPick) = d.Picks.Open(d.Clock.GetUtcNow());
        var leave = new HashSet<TodoId>(todos);
        if (holdPick && hold is not null)
        {
            leave.UnionWith(hold.Changes.Select(static c => c.Id));
        }

        if (holdPick)
        {
            leave.UnionWith(picked.Held);
        }

        var open = rows.Count(TodoDecisions.IsOpen);
        var room = NoteCounter.WillImport(NoteCounter.MaxOpen, open);
        return new NoteMergeOptions(_minter, d.Scrubber)
        {
            PickBases = picked.Bases.Where(p => !leave.Contains(p.Key)).ToDictionary(static p => p.Key, static p => p.Value),
            LeaveAlone = leave,
            PickedRemoved = picked.Removed.Where(id => !leave.Contains(id)).ToHashSet(),
            OpenConflicts = rows.Where(static r => r.Conflict is not null).ToDictionary(static r => r.Id, static r => RowMapping.ToSide(r.Conflict!.Obsidian)),   // why: filtered on the line before
            OpenHold = hold is null ? null : new NoteHold(hold.Changes),
            MaxAdoptions = oneWay ? 0 : mode == PassMode.Import ? room : Math.Min(AdoptPerPass, room),
        };
    }

    // REQ-S3: a note that is missing, unreadable or 0 bytes while the base has lines is "can't read", never "every line was deleted"
    private static string? Unreadable(NoteRead read, NoteBase? noteBase)
    {
        var hasBase = noteBase is { Lines.Count: > 0 };
        return read.Problem is { } problem ? "can't read the note: " + problem
            : read.Missing && hasBase ? "the note is missing"
            : !read.Missing && read.Bytes == 0 && hasBase ? "the note is empty"
            : null;
    }

    private async ValueTask<Result<bool>> Waiting(PassInputs inputs, NoteDocument doc, CancellationToken cancellationToken)
    {
        if (inputs.Base is not null || mode == PassMode.Import || NoteCounter.Count(doc).Open == 0)
        {
            return false;
        }

        var imported = await _ledger.Imported(place, cancellationToken).ConfigureAwait(false);
        return imported.IsError ? imported.Error : !imported.Value;
    }
}
