using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>
/// Appends what a pass decided (todo spec §4.1): the note's edits as events 'by: obsidian' under the version key, adopted lines' 'todo.added' under 'todo.added:&lt;id&gt;', the conflicts, the hold and the pass's own
/// bookkeeping 'by: service:todo-note'. One instance per attempt: it counts the versions it moved, and it skips an intent it already appended, so a retry swap that merges the same edit again writes it once.
/// A lost version key is SND-TODO-409, which the pass answers with a fresh read.
/// </summary>
/// <param name="d">What a pass reaches.</param>
/// <param name="versions">Each todo's version as the rows have it.</param>
/// <param name="noteHash">The hash of the note the intents were drawn from, as events carry it.</param>
internal sealed class NoteEvents(SyncDeps d, IReadOnlyDictionary<TodoId, long> versions, string noteHash)
{
    private const string FromNote = "obsidian";
    private const string Service = "service:todo-note";

    private readonly Dictionary<TodoId, long> _versions = new(versions);
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>Gets how many events were appended.</summary>
    public int Count { get; private set; }

    /// <summary>Appends the events of every intent but the adoptions.</summary>
    /// <param name="intents">The merge's intents.</param>
    /// <param name="note">The note they were drawn from.</param>
    /// <param name="cancellationToken">Cancels the appends.</param>
    /// <returns>Unit, or the first error.</returns>
    public async ValueTask<Result<Unit>> Changes(IReadOnlyList<NoteIntent> intents, NoteDocument note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intents);
        foreach (var intent in intents.Where(static i => i is not NoteAdopt))
        {
            if (!_seen.Add(Signature(intent)))
            {
                continue;
            }

            var put = await Apply(intent, note, cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                return put.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>Appends 'todo.added' for each adopted line, once the write that gave it an id has ended clean.</summary>
    /// <param name="intents">The merge's intents.</param>
    /// <param name="cancellationToken">Cancels the appends.</param>
    /// <returns>Unit, or the first error.</returns>
    public async ValueTask<Result<Unit>> Adopts(IReadOnlyList<NoteIntent> intents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intents);
        foreach (var adopt in intents.OfType<NoteAdopt>())
        {
            var added = await Added(new Fresh(adopt.Id, adopt.Text, adopt.Due, adopt.Links, adopt.Done), cancellationToken).ConfigureAwait(false);
            if (added.IsError)
            {
                return added.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>Appends what the pass found about the hold.</summary>
    /// <param name="merged">The merge.</param>
    /// <param name="open">The hold that was open before the pass, or null.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Unit, or the error.</returns>
    public async ValueTask<Result<Unit>> Hold(NoteMergeResult merged, TodoOpenHold? open, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(merged);
        Result<bool> put = false;
        if (merged.Hold is NoteHoldOutcome.Opened or NoteHoldOutcome.Replaced && merged.HoldNow is { } now)
        {
            var changes = now.Changes.Select(c => new TodoHeldChange(c.Id, Kind(c.Kind), c.NoteLine is null ? null : d.Scrubber.Scrub(c.NoteLine))).ToList();
            put = await Put(EventTypes.TodoNoteHeld, new TodoNoteHeld(changes, noteHash, Service, OpenInBase: now.OpenInBase), $"todo.note.held:{noteHash}", versioned: false, cancellationToken).ConfigureAwait(false);
        }
        else if (merged.Hold == NoteHoldOutcome.Dissolved && open is not null)
        {
            var resolved = new TodoNoteHeldResolved("dissolved", open.NoteHash, open.Changes.Count, Service);
            put = await Put(EventTypes.TodoNoteHeldResolved, resolved, $"todo.note.held.resolved:{open.NoteHash}:{d.Ids.NewId():N}", versioned: false, cancellationToken).ConfigureAwait(false);
        }

        return put.IsError ? put.Error : Unit.Value;
    }

    /// <summary>Appends 'todo.note.synced'.</summary>
    /// <param name="place">The note.</param>
    /// <param name="synced">The payload's hash, base hash and counts.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Unit, or the error.</returns>
    public async ValueTask<Result<Unit>> Synced(NotePlace place, TodoNoteSynced synced, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(synced);
        var put = await Put(EventTypes.TodoNoteSynced, synced, $"todo.note.synced:{synced.Hash}:{d.Ids.NewId():N}", versioned: false, cancellationToken).ConfigureAwait(false);
        return put.IsError ? put.Error : Unit.Value;
    }

    /// <summary>Appends 'todo.note.imported'.</summary>
    /// <param name="imported">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Unit, or the error.</returns>
    public async ValueTask<Result<Unit>> Imported(TodoNoteImported imported, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imported);
        var put = await Put(EventTypes.TodoNoteImported, imported, $"todo.note.imported:{imported.Vault}:{d.Ids.NewId():N}", versioned: false, cancellationToken).ConfigureAwait(false);
        return put.IsError ? put.Error : Unit.Value;
    }

    private ValueTask<Result<Unit>> Apply(NoteIntent intent, NoteDocument note, CancellationToken cancellationToken) => intent switch
    {
        NoteChange c => Versioned(c.Id, EventTypes.TodoChanged, v => new TodoChanged(c.Id, v, c.Text, c.DueChanged ? c.Due : null, c.DueChanged && c.Due is null, c.Links, FromNote), cancellationToken),
        NoteDone x => Versioned(x.Id, EventTypes.TodoDone, v => new TodoDone(x.Id, v, FromNote), cancellationToken),
        NoteReopen x => Versioned(x.Id, EventTypes.TodoReopened, v => new TodoReopened(x.Id, v, FromNote), cancellationToken),
        NoteRemove x => Versioned(x.Id, EventTypes.TodoRemoved, v => new TodoRemoved(x.Id, v, FromNote), cancellationToken),
        NoteRestore x => Versioned(x.Id, EventTypes.TodoRestored, v => new TodoRestored(x.Id, v, FromNote), cancellationToken),
        NoteAdd a => Added(new Fresh(a.Id, a.Text, a.Due, a.Links, a.Done), cancellationToken),
        NoteConflict c => Raise(c, cancellationToken),
        NoteConflictSettled s => Settle(s.Id, note, cancellationToken),
        _ => ValueTask.FromResult<Result<Unit>>(Unit.Value),
    };

    private async ValueTask<Result<Unit>> Versioned<TPayload>(TodoId id, string type, Func<long, TPayload> payload, CancellationToken cancellationToken)
    {
        var version = _versions.GetValueOrDefault(id, 0L) + 1;
        var put = await Put(type, payload(version), TodoWrite.VersionKey(id, version), versioned: true, cancellationToken).ConfigureAwait(false);
        if (put.IsError)
        {
            return put.Error;
        }

        _versions[id] = version;
        return Unit.Value;
    }

    private sealed record Fresh(TodoId Id, string Text, DateOnly? Due, IReadOnlyList<string> Links, bool Done);

    private async ValueTask<Result<Unit>> Added(Fresh fresh, CancellationToken cancellationToken)
    {
        var id = fresh.Id;
        var added = new TodoAdded(id, fresh.Text, fresh.Due, fresh.Links, ClientId: null, FromNote);
        var put = await Put(EventTypes.TodoAdded, added, $"todo.added:{id.Value}", versioned: false, cancellationToken).ConfigureAwait(false);
        if (put.IsError)
        {
            return put.Error;
        }

        _versions[id] = 1;
        return fresh.Done && put.Value ? await Versioned(id, EventTypes.TodoDone, v => new TodoDone(id, v, FromNote), cancellationToken).ConfigureAwait(false) : Unit.Value;
    }

    private async ValueTask<Result<Unit>> Raise(NoteConflict conflict, CancellationToken cancellationToken)
    {
        var raised = new TodoConflicted(conflict.Id, RowMapping.ToEventSide(conflict.Sindri), RowMapping.ToEventSide(conflict.Obsidian), noteHash);
        var put = await Put(EventTypes.TodoConflicted, raised, $"todo.conflicted:{conflict.Id.Value}:{noteHash}", versioned: false, cancellationToken).ConfigureAwait(false);
        return put.IsError ? put.Error : Unit.Value;
    }

    private async ValueTask<Result<Unit>> Settle(TodoId id, NoteDocument note, CancellationToken cancellationToken)
    {
        var line = note.Todos.FirstOrDefault(t => id.Equals(t.Id))?.Raw ?? string.Empty;
        line = d.Scrubber.Scrub(line);   // the ledger never sees what the pass scrubs on the way to the base (REQ-W2)
        var settled = new TodoConflictResolved(id, TodoSideName.Sindri, line, Service);
        var put = await Put(EventTypes.TodoConflictResolved, settled, $"todo.conflict.resolved:{id.Value}:{d.Ids.NewId():N}", versioned: false, cancellationToken).ConfigureAwait(false);
        return put.IsError ? put.Error : Unit.Value;
    }

    private async ValueTask<Result<bool>> Put<TPayload>(string type, TPayload payload, string key, bool versioned, CancellationToken cancellationToken)
    {
        if (d.Held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await d.Ledger.Append(d.Events.Entry(type, payload, key), grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        if (appended.Value.AlreadyRecorded)
        {
            return versioned ? SindriErrors.TodoConflict.With(TodoDecisions.StaleText) : false;
        }

        Count++;
        return true;
    }

    private static string Kind(HeldKind kind) => kind switch
    {
        HeldKind.Removed => "removed",
        HeldKind.Edited => "edited",
        HeldKind.Reopened => "reopened",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a held kind"),
    };

    // why: an intent is a record with lists, which compare by reference; this is what makes two merges of the same edit the same intent
    private static string Signature(NoteIntent intent) => $"{intent.GetType().Name}|{intent.Id.Value}|{Fields(intent)}";

    private static string Fields(NoteIntent intent) => intent switch
    {
        NoteChange c => string.Create(CultureInfo.InvariantCulture, $"{c.Text}|{c.DueChanged}|{c.Due}|{(c.Links is null ? "~" : string.Join(',', c.Links))}"),
        NoteAdd a => string.Create(CultureInfo.InvariantCulture, $"{a.Text}|{a.Due}|{a.Done}|{string.Join(',', a.Links)}"),
        NoteConflict c => string.Create(CultureInfo.InvariantCulture, $"{c.Obsidian.Text}|{c.Obsidian.Due}|{c.Obsidian.Done}|{c.Obsidian.Removed}"),
        _ => string.Empty,
    };
}
