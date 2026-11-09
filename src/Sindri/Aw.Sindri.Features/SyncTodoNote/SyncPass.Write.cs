using Aw.Kernel;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.SyncTodoNote;

internal sealed partial class SyncPass
{
    private const string NoSwap = "can't write to this disk";

    /// <summary>One round of the write loop: the note the build is drawn from, the merge, the hash of what is at the note path now, and which swap this is.</summary>
    private sealed record WriteState(NoteDocument Doc, NoteMergeResult Result, string Expected, bool ExpectedEmpty, int Swaps, PassInputs In);

    /// <summary>How a round ended: the write's end, or the next round's state.</summary>
    private sealed record RoundEnd(WriteEnd? End, WriteState? Next);

    /// <summary>What a round built: the text and its hash.</summary>
    private sealed record Built(string Text, string Hash);

    // Writing steps 1-3: build, record the intent, swap, wait for what came out and compare it with the note the build was drawn from
    private async ValueTask<Result<WriteEnd>> WriteBack(Merged m, NoteEvents events, CancellationToken cancellationToken)
    {
        var state = new WriteState(m.Doc, m.Result, m.Read.Hash ?? RowMapping.HashOf(string.Empty), m.Read.Bytes == 0, Swaps: 1, m.In);
        while (true)
        {
            var round = await Round(state, events, cancellationToken).ConfigureAwait(false);
            if (round.IsError)
            {
                return round.Error;
            }

            if (round.Value.End is { } end)
            {
                return end;
            }

            state = round.Value.Next!;   // why: a round with no end carries the next state
        }
    }

    private async ValueTask<Result<RoundEnd>> Round(WriteState state, NoteEvents events, CancellationToken cancellationToken)
    {
        var text = NoteRenderer.Rebuild(state.Doc, state.Result.LineEdits);
        var built = new Built(text, RowMapping.HashOf(text));
        d.Files.WriteIntent(built.Hash);
        var swap = d.Files.Swap(place.Path, text);
        if (swap.Problem is { } problem)
        {
            d.Files.ClearIntent();
            var stop = string.Equals(problem, NoSwap, StringComparison.Ordinal) ? new SyncOutcome(TodoNoteState.OneWay, Error: null) : new SyncOutcome(TodoNoteState.Syncing, "can't write the note: " + problem);
            return new RoundEnd(new WriteEnd(Landed: null, stop), Next: null);
        }

        return swap.CameOut is { } cameOut
            ? await Settle(state, built, cameOut, events, cancellationToken).ConfigureAwait(false)
            : new RoundEnd(new WriteEnd(new Landed(state.Doc, state.Result, built.Hash), Stop: null), Next: null);
    }

    // step 3: what came out equals the note we merged (done), differs (Obsidian saved in between: merge it as the note, same base and same minted ids, and write again) or is empty
    private async ValueTask<Result<RoundEnd>> Settle(WriteState state, Built built, string cameOut, NoteEvents events, CancellationToken cancellationToken)
    {
        var (came, why) = await CameOut(cameOut, state.ExpectedEmpty, cancellationToken).ConfigureAwait(false);
        if (came is null)
        {
            return new RoundEnd(new WriteEnd(Landed: null, new SyncOutcome(TodoNoteState.CantRead, why)), Next: null);
        }

        if (string.Equals(came.Hash, state.Expected, StringComparison.Ordinal))
        {
            d.Files.Delete(cameOut);
            return new RoundEnd(new WriteEnd(new Landed(state.Doc, state.Result, built.Hash), Stop: null), Next: null);
        }

        if (state.Swaps == MaxSwaps)
        {
            return new RoundEnd(new WriteEnd(Landed: null, new SyncOutcome(TodoNoteState.Busy, Error: null)), Next: null);
        }

        var doc = NoteParser.Parse(came.Text ?? string.Empty);
        var result = NoteMerge.Merge(state.In.Base, doc, state.In.Sindri, state.In.Options);
        var more = await events.Changes(result.Intents, doc, cancellationToken).ConfigureAwait(false);
        if (more.IsError)
        {
            return more.Error;
        }

        d.Files.Delete(cameOut);   // merged: the next swap needs the tmp path free
        return new RoundEnd(End: null, new WriteState(doc, result, built.Hash, built.Text.Length == 0, state.Swaps + 1, state.In));
    }

    // the settle, and the 0-byte exit: an empty file is never read as a deletion; after 30 s still empty it is kept and the pass stops
    private async ValueTask<(NoteRead? Read, string? Problem)> CameOut(string path, bool expectedEmpty, CancellationToken cancellationToken)
    {
        var read = (await d.Files.ReadQuiet(path, _quiet, _longest, cancellationToken).ConfigureAwait(false)).Read;
        if (read.Bytes == 0 && read.Problem is null && !read.Missing && !expectedEmpty)
        {
            read = (await d.Files.ReadQuiet(path, _longest, _longest, cancellationToken).ConfigureAwait(false)).Read;
            if (read.Bytes == 0 && read.Problem is null && !read.Missing)
            {
                _ = d.Files.Keep(path, d.Clock.GetUtcNow());
                return (null, "the note came out of the swap empty; it is kept beside the note as .sindri-tmp.keep");
            }
        }

        return read.Problem is { } problem ? (null, "can't read what came out of the swap: " + problem)
            : read.Missing ? (null, "what came out of the swap is gone")
            : (read, null);
    }
}
