using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// Decision extraction for the records one feed holds in the owned record store (owned-index spec §4.2, §5.7 D11): a tracker record that quotes a decision becomes a decision node
/// citing it, like a Linear or Slack record does (#1373). It runs right after the feed's <see cref="RecordSyncRun"/> tick, under the same lock, never inside ingest: each stored record
/// (not removed, not blocked) that is not done yet is written as an intent row with the same key (so the overlay can cite it), linked by <see cref="RulesLinker"/> against the
/// map from its stored, already scrubbed chunks, and read by <see cref="DecisionPass"/>. Done means this version of the record is stored in the intent layer and not waiting for
/// extraction, whatever the map version: one model call per record version, so a replay and a map rebuild ask the model about nothing for unchanged records. Their intent links stay as last
/// computed (the map build links owned records to code and features itself, RecordPass) and are refreshed when the record changes. The intent source cursor is left alone (the page's cursor is null).
/// </summary>
internal sealed class RecordDecisionRun(SourceId source, VisibleMap map, (ContextSettings Settings, Scrubbers Scrubbers) context, (IRecordStore Records, IIntentStore Intents, IModelPort Model, TimeProvider Clock) ports, IReadOnlyList<PinAliases> pins)
{
    private const int PageSize = 100;

    private readonly DateTimeOffset _started = ports.Clock.GetUtcNow();
    private int _fetched;
    private int _linked;
    private int _unlinked;

    private DecisionPass Decisions => field ??= new(ports.Intents, ports.Model, context.Settings.Labeling, (context.Scrubbers.Ingest, ports.Clock));

    /// <summary>Reads the feed's new records for decisions and folds the numbers into the feed's row; a decision-pass failure code wins over the cap.</summary>
    public async ValueTask<SourceSync> Execute(SourceSync row, CancellationToken cancellationToken)
    {
        var layer = await ports.Intents.Layer(cancellationToken).ConfigureAwait(false);
        var done = layer.IsError ? layer.Error : await Done(layer.Value, cancellationToken).ConfigureAwait(false);
        if (done.IsError)
        {
            return row with { Code = row.Code ?? done.Error.Code };
        }

        var code = await Walk(done.Value, layer.Value.Records.Where(r => r.Source == source).ToDictionary(r => r.Key.Value, StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
        code = Outcome(row.Code, code);
        var state = await ports.Intents.State(source, cancellationToken).ConfigureAwait(false);
        return row with
        {
            Linked = _linked,
            Unlinked = _unlinked,
            Decisions = state.IsError ? 0 : state.Value.Decisions,
            Queued = state.IsError ? 0 : state.Value.Queued,
            Refused = Decisions.Refused,
            Tokens = Decisions.Tokens,
            Code = code,
        };
    }

    /// <summary>The row's code: the tick's own failure stays unless the pass has one; a real failure of the decision pass wins over 059 (the cap would hide an outage as "ok, the next run continues"); 042 does not.</summary>
    private string? Outcome(string? tick, string? walk) =>
        walk == ContextErrors.SyncCapped.Code ? Decisions.Failure ?? walk : walk ?? Decisions.Failure ?? tick ?? (Decisions.HeldBack ? ContextErrors.LabelingUnavailable.Code : null);

    /// <summary>The versions the intent layer already holds and that are not waiting for extraction: those records are skipped. The map version does not matter.</summary>
    private async ValueTask<Result<Dictionary<string, string>>> Done(IntentLayer layer, CancellationToken cancellationToken)
    {
        var queued = context.Settings.Labeling.Enabled ? await ports.Intents.Queued(source, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<RecordKey>>([]);
        if (queued.IsError)
        {
            return queued.Error;
        }

        var waiting = queued.Value.Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
        return Results.Success(new Dictionary<string, string>(
            layer.Records.Where(r => r.Source == source && !waiting.Contains(r.Key.Value)).Select(r => KeyValuePair.Create(r.Key.Value, r.Version)), StringComparer.Ordinal));
    }

    private async ValueTask<string?> Walk(Dictionary<string, string> done, Dictionary<string, IntentRecord> known, CancellationToken cancellationToken)
    {
        RecordKey? after = null;
        while (true)
        {
            var page = await ports.Records.List(new RecordFilter(Source: source), after, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error.Code;
            }

            var stopped = await Page(page.Value, (done, known), cancellationToken).ConfigureAwait(false);
            if (stopped is not null)
            {
                return stopped;
            }

            if (page.Value.Count < PageSize)
            {
                return null;
            }

            after = page.Value[^1].Key;
        }
    }

    private async ValueTask<string?> Page(IReadOnlyList<StoredRecord> page, (Dictionary<string, string> Done, Dictionary<string, IntentRecord> Known) seen, CancellationToken cancellationToken)
    {
        foreach (var stored in page.Where(r => r.RemovedAt is null && !r.Blocked))
        {
            if (seen.Done.TryGetValue(stored.Key.Value, out var version) && string.Equals(version, stored.Version.Value, StringComparison.Ordinal))
            {
                var aliased = await Realias(stored, seen.Known, cancellationToken).ConfigureAwait(false);   // aliases only: no requeue, no model call
                if (aliased is not null)
                {
                    return aliased;
                }

                continue;
            }

            if (_fetched > 0 && ports.Clock.GetUtcNow() - _started >= SyncSourcesService.RunCap)
            {
                return ContextErrors.SyncCapped.Code;   // every run completes at least one record; the rest wait for the next
            }

            var failed = await One(stored, seen.Known, cancellationToken).ConfigureAwait(false);
            if (failed is not null)
            {
                return failed;
            }
        }

        return null;
    }

    /// <summary>Links, commits and extracts one record; a code when the run must stop.</summary>
    private async ValueTask<string?> One(StoredRecord stored, Dictionary<string, IntentRecord> known, CancellationToken cancellationToken)
    {
        var chunks = await ports.Records.Chunks(stored.Key, cancellationToken).ConfigureAwait(false);
        if (chunks.IsError)
        {
            return chunks.Error.Code;
        }

        var text = string.Join('\n', chunks.Value.OrderBy(c => c.Seq).Select(c => c.Text));   // scrubbed at ingest
        var record = new IntentRecord(stored.Key, source, stored.Version.Value, stored.Title, Aliases(stored)) { CreatedAt = stored.CreatedAt };
        var before = known.GetValueOrDefault(record.Key.Value);
        known[record.Key.Value] = record;
        var found = RulesLinker.Link(record.Key, text, map.Graph, ([.. known.Values], new HashSet<string>(context.Settings.LinearTeams, StringComparer.Ordinal)), pins);
        _fetched++;
        _linked += found.Count;
        _unlinked += found.Count == 0 ? 1 : 0;

        var page = new IntentPage(source, [record], found, Cursor: null, ports.Clock.GetUtcNow()) { MapVersion = map.Snapshot.Version.Number };
        var committed = await ports.Intents.CommitPage(page, cancellationToken).ConfigureAwait(false);
        if (committed.IsError)
        {
            return committed.Error.Code;
        }

        if (before is not null && string.Equals(before.Version, record.Version, StringComparison.Ordinal) && !before.Aliases.SequenceEqual(record.Aliases, StringComparer.Ordinal))
        {
            var aliased = await ports.Intents.SetAliases(record.Key, record.Version, record.Aliases, cancellationToken).ConfigureAwait(false);   // CommitPage's upsert skips a same-version row
            if (aliased.IsError)
            {
                return aliased.Error.Code;
            }
        }

        await Decisions.Run([new PendingRecord(record, text, found)], cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>Writes only the aliases of a done record whose stored ones differ from the permalink's identifier; a code when the run must stop.</summary>
    private async ValueTask<string?> Realias(StoredRecord stored, Dictionary<string, IntentRecord> known, CancellationToken cancellationToken)
    {
        var aliases = Aliases(stored);
        if (!known.TryGetValue(stored.Key.Value, out var current) || current.Aliases.SequenceEqual(aliases, StringComparer.Ordinal))
        {
            return null;
        }

        var written = await ports.Intents.SetAliases(stored.Key, stored.Version.Value, aliases, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error.Code;
        }

        known[stored.Key.Value] = current with { Aliases = aliases };
        return null;
    }

    /// <summary>A Linear issue's tracker identifier, read from its stored permalink (the feed stores the plain title); nothing for any other record.</summary>
    private string[] Aliases(StoredRecord stored) => LinearIdentifiers.AliasesOf(stored.Key, stored.Url, new HashSet<string>(context.Settings.LinearTeams, StringComparer.Ordinal));
}
