using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.SyncTodoNote;

internal sealed partial class SyncPass
{
    /// <summary>The note as read and what the merge made of it.</summary>
    private sealed record Merged(PassInputs In, NoteDocument Doc, NoteRead Read, NoteMergeResult Result);

    /// <summary>The note and merge the pass ended on, and the hash of what it wrote (null when it wrote nothing).</summary>
    private sealed record Landed(NoteDocument Doc, NoteMergeResult Result, string? Hash);

    /// <summary>How a write ended: on the last merge, or stopped with an outcome.</summary>
    private sealed record WriteEnd(Landed? Landed, SyncOutcome? Stop);

    private async ValueTask<Result<SyncOutcome>> Land(Merged m, CancellationToken cancellationToken)
    {
        var readHash = m.Read.Hash ?? RowMapping.HashOf(string.Empty);
        var events = new NoteEvents(d, m.In.Rows.ToDictionary(static r => r.Id, static r => r.Version), RowMapping.Tag(readHash));
        var changed = await events.Changes(m.Result.Intents, m.Doc, cancellationToken).ConfigureAwait(false);
        var held = changed.IsError ? changed : await events.Hold(m.Result, m.In.Hold, cancellationToken).ConfigureAwait(false);
        if (held.IsError)
        {
            return held.Error;
        }

        var landed = new Landed(m.Doc, m.Result, Hash: null);
        if (!m.In.OneWay && m.Result.LineEdits.Count > 0)
        {
            var wrote = await WriteBack(m, events, cancellationToken).ConfigureAwait(false);
            if (wrote.IsError)
            {
                return wrote.Error;
            }

            if (wrote.Value.Stop is { } stop)
            {
                return stop with { Events = events.Count, NoteHash = readHash };
            }

            landed = wrote.Value.Landed ?? landed;
        }

        var adopted = await events.Adopts(landed.Result.Intents, cancellationToken).ConfigureAwait(false);
        return adopted.IsError ? adopted.Error : await Record(m, landed, events, cancellationToken).ConfigureAwait(false);
    }

    // step 4: the base, then 'todo.note.synced', then the intent file goes; a pass that changed nothing appends nothing
    private async ValueTask<Result<SyncOutcome>> Record(Merged m, Landed landed, NoteEvents events, CancellationToken cancellationToken)
    {
        var lines = m.In.OneWay ? [.. m.Doc.Todos.Select(t => d.Scrubber.Scrub(t.Raw))] : landed.Result.NextBase;
        var outcome = Describe(m, landed);
        var changed = events.Count > 0 || outcome.Written > 0 || m.In.Base is null || !lines.SequenceEqual(m.In.Base.Lines, StringComparer.Ordinal);
        if (!changed)
        {
            return outcome;
        }

        var baseHash = d.Files.SaveBase(new NoteBase(lines));
        var synced = new TodoNoteSynced(place.Vault, place.Note, RowMapping.Tag(landed.Hash ?? outcome.NoteHash), baseHash, outcome.LinesRead, outcome.Written, "service:todo-note");
        var put = await events.Synced(place, synced, cancellationToken).ConfigureAwait(false);
        if (put.IsError)
        {
            return put.Error;
        }

        d.Files.ClearIntent();
        d.Files.DeleteBases(baseHash);
        return outcome with { Events = events.Count };
    }

    private static SyncOutcome Describe(Merged m, Landed landed) =>
        new(m.In.OneWay ? TodoNoteState.OneWay : TodoNoteState.Syncing, Error: null)
        {
            NotAdopted = m.Result.Deferred,
            Written = landed.Hash is null ? 0 : landed.Result.LineEdits.Count,
            Conflicts = m.Result.Intents.OfType<NoteConflict>().Count(),
            LinesRead = m.Doc.Todos.Count(),
            Adopted = landed.Result.Intents.OfType<NoteAdopt>().Count(static a => !a.Done),   // why: an import counts the open lines it took
            NoteHash = m.Read.Hash ?? RowMapping.HashOf(string.Empty),
        };

    // step 5 of Writing: a tmp file left by an earlier run. The swap never ran when its hash is the intent's (delete it unread); else it is what came out, merged as the note (its events and adoptions only; the pass then reads the note as it is)
    private async ValueTask<Result<SyncOutcome?>> Leftover(PassInputs inputs, CancellationToken cancellationToken)
    {
        if (d.Files.Leftover(place.Path) is not { } tmp)
        {
            return Results.Success<SyncOutcome?>(value: null);
        }

        var read = d.Files.Read(tmp);
        if (read.Missing)
        {
            return Results.Success<SyncOutcome?>(value: null);
        }

        if (read.Problem is { } problem)
        {
            return new SyncOutcome(TodoNoteState.CantRead, "can't read what the last swap left: " + problem);
        }

        var intent = d.Files.ReadIntent();
        if (intent is null || string.Equals(read.Hash, intent, StringComparison.Ordinal))
        {
            d.Files.Delete(tmp);
            return Results.Success<SyncOutcome?>(value: null);
        }

        if (read.Bytes == 0)
        {
            _ = d.Files.Keep(tmp, d.Clock.GetUtcNow());
            return new SyncOutcome(TodoNoteState.CantRead, "the file the last swap left was empty; it is kept beside the note");
        }

        var merged = await MergeLeftover(inputs, read, cancellationToken).ConfigureAwait(false);
        if (merged.IsError)
        {
            return merged.Error;
        }

        d.Files.Delete(tmp);
        return Results.Success<SyncOutcome?>(value: null);
    }

    // what came out of the last swap, merged as the note: its changes and the lines it adopts become events
    private async ValueTask<Result<Unit>> MergeLeftover(PassInputs inputs, NoteRead read, CancellationToken cancellationToken)
    {
        var doc = NoteParser.Parse(read.Text ?? string.Empty);
        var events = new NoteEvents(d, inputs.Rows.ToDictionary(static r => r.Id, static r => r.Version), RowMapping.Tag(read.Hash ?? string.Empty));
        var intents = NoteMerge.Merge(inputs.Base, doc, inputs.Sindri, inputs.Options).Intents;
        var put = await events.Changes(intents, doc, cancellationToken).ConfigureAwait(false);
        return put.IsError ? put : await events.Adopts(intents, cancellationToken).ConfigureAwait(false);   // why: a line typed into what came out of the last swap is a new todo; without this it went with the tmp file
    }
}
