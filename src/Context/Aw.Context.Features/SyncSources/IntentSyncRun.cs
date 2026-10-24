using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// One live intent source against the current map (spec §8.2 step 3 sub-queries, §6.2): each map node's terms are searched, the hits whose
/// version is new are fetched, cleaned at the ingest boundary and linked by <see cref="RulesLinker"/>. Each record is committed with its links and its
/// decisions are extracted before the next one is fetched, so a run that the time cap stops has finished every record it fetched, and the next run skips them
/// as unchanged (a slow source can't starve: every run completes at least one record). A node's cursor 'mapVersion:nodesDone' moves when the node is through,
/// so a stopped run (the source down, the time cap) resumes where it was and a replay writes nothing new. A map version change re-links every record a node's search still returns (one no node finds any more keeps its old links),
/// across runs: a record is skipped as unchanged only when its upstream version is the stored one and its links were computed against this map version
/// (<see cref="IntentPage.MapVersion"/>), so a run that stops early doesn't leave the records it never reached with their old-map links, and no record is re-fetched twice for one map.
/// </summary>
internal sealed class IntentSyncRun(IContextSource source, VisibleMap map, (ContextSettings Settings, Scrubbers Scrubbers) context, (IIntentStore Store, IModelPort Model, TimeProvider Clock) ports)
{
    private readonly SourceId _id = source.Describe().Id;
    private readonly MapText _text = new(context.Scrubbers);
    private readonly DateTimeOffset _started = ports.Clock.GetUtcNow();
    private int _fetched;
    private int _unchanged;
    private int _linked;
    private int _unlinked;

    private DecisionPass Decisions => field ??= new(ports.Store, ports.Model, context.Settings.Labeling, (context.Scrubbers.Ingest, ports.Clock));

    private bool OverCap() => ports.Clock.GetUtcNow() - _started >= SyncSourcesService.RunCap;

    public async ValueTask<SourceSync> Execute(CancellationToken cancellationToken)
    {
        var layer = await ports.Store.Layer(cancellationToken).ConfigureAwait(false);
        var state = await ports.Store.State(_id, cancellationToken).ConfigureAwait(false);
        if (layer.IsError || state.IsError)
        {
            return Row(0, (layer.IsError ? layer : state.Bind(_ => layer)).Error.Code);
        }

        var version = map.Snapshot.Version.Number;
        var nodes = map.Graph.Nodes.Where(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision)).OrderBy(n => n.Key.Value, StringComparer.Ordinal).ToList();
        var offset = Resume(state.Value.Cursor, version, nodes.Count);
        var known = layer.Value.Records.Where(r => r.Source == _id).ToDictionary(r => r.Key.Value, StringComparer.Ordinal);
        var stored = await StoredVersions(known, version, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return Row(0, stored.Error.Code);   // as for Layer and State above; reading it as "nothing linked" would fetch and re-extract every record
        }

        var code = await Walk(nodes, offset, (version, stored.Value, known), cancellationToken).ConfigureAwait(false);
        code = Outcome(code);
        if (code is not null)
        {
            _ = await ports.Store.RecordFailure(_id, code, cancellationToken).ConfigureAwait(false);
        }

        return await Summary(code, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The run's code: a real failure of the decision pass wins over 059 (the time cap is only partial, and would hide an outage as "ok, the next run continues"); 042 does not.</summary>
    private string? Outcome(string? code) =>
        code == ContextErrors.SyncCapped.Code ? Decisions.Failure ?? code : code ?? Decisions.Failure ?? (Decisions.HeldBack ? ContextErrors.LabelingUnavailable.Code : null);

    /// <summary>
    /// The versions already linked under this map version; records linked under an older map are left out, so they are fetched and linked again, and so are records still
    /// waiting for extraction (their text isn't stored).
    /// </summary>
    private async ValueTask<Result<Dictionary<string, string>>> StoredVersions(Dictionary<string, IntentRecord> known, int mapVersion, CancellationToken cancellationToken)
    {
        var current = await ports.Store.LinkedUnder(_id, mapVersion, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var linkedNow = current.Value.Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
        var queued = context.Settings.Labeling.Enabled ? await ports.Store.Queued(_id, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<RecordKey>>([]);
        if (queued.IsError)
        {
            return queued.Error;   // as for LinkedUnder: reading it as "nothing waiting" would skip a record still waiting for extraction as unchanged
        }

        var waiting = queued.Value.Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
        return Results.Success(new Dictionary<string, string>(known.Where(p => linkedNow.Contains(p.Key) && !waiting.Contains(p.Key)).Select(p => KeyValuePair.Create(p.Key, p.Value.Version)), StringComparer.Ordinal));
    }

    private static int Resume(string? cursor, int version, int total)
    {
        var parts = cursor?.Split(':');
        var read = parts is [var mv, var done] && int.TryParse(mv, NumberStyles.None, CultureInfo.InvariantCulture, out var v) && int.TryParse(done, NumberStyles.None, CultureInfo.InvariantCulture, out var d)
            ? (v, d)
            : (-1, 0);
        return read.Item1 == version && read.Item2 > 0 && read.Item2 < total ? read.Item2 : 0;
    }

    private async ValueTask<string?> Walk(List<GraphNode> nodes, int offset, (int Version, Dictionary<string, string> Stored, Dictionary<string, IntentRecord> Known) seen, CancellationToken ct)
    {
        var fetchedThisRun = new HashSet<string>(StringComparer.Ordinal);
        for (var i = offset; i < nodes.Count; i++)
        {
            if (OverCap())
            {
                return ContextErrors.SyncCapped.Code;
            }

            var terms = _text.SearchTerms(nodes[i].Label);
            if (terms.Count == 0)
            {
                continue;
            }

            var stopped = await Node(terms, i, (seen, fetchedThisRun), ct).ConfigureAwait(false);
            if (stopped is not null)
            {
                return stopped;
            }
        }

        // The walk is through: the cursor says so even when the last nodes had no terms (so no page moved it), and the next run starts at the first node.
        var finished = await ports.Store.CommitPage(new IntentPage(_id, [], [], string.Create(CultureInfo.InvariantCulture, $"{seen.Version}:{nodes.Count}"), ports.Clock.GetUtcNow()), ct).ConfigureAwait(false);
        return finished.IsError ? finished.Error.Code : null;
    }

    /// <summary>Searches one node, then fetches, commits and extracts its new hits one record at a time; a code when the run must stop.</summary>
    private async ValueTask<string?> Node(IReadOnlyList<string> terms, int index, ((int Version, Dictionary<string, string> Stored, Dictionary<string, IntentRecord> Known) Seen, HashSet<string> Fetched) walk, CancellationToken ct)
    {
        var hits = await Within(t => source.Search(new SourceQuery(terms, AsOf: null, Limit: SourceQuery.DefaultLimit, context.Settings.Deadline), t), ct).ConfigureAwait(false);
        if (hits.IsError)
        {
            return hits.Error.Code;
        }

        var (stopped, failure) = await Pull([.. hits.Value.Where(h => walk.Fetched.Add(h.Ref.Canonical.Value))], walk.Seen, ct).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        var cursor = string.Create(CultureInfo.InvariantCulture, $"{walk.Seen.Version}:{(stopped ? index : index + 1)}");   // a node the cap cut short is walked again: its stored hits are skipped as unchanged
        var committed = await ports.Store.CommitPage(new IntentPage(_id, [], [], cursor, ports.Clock.GetUtcNow()), ct).ConfigureAwait(false);
        return committed.IsError ? committed.Error.Code : stopped ? ContextErrors.SyncCapped.Code : null;   // what was committed stays; a held-back record stays queued for the next run
    }

    /// <summary>
    /// Fetches the hits whose version is new, one at a time: each record is committed with its links and its decisions extracted before the next fetch, so the cap
    /// (checked before each fetch after the run's first, so every run completes a record) never leaves a fetched record without its extraction.
    /// </summary>
    private async ValueTask<(bool Stopped, string? Code)> Pull(IReadOnlyList<SourceHit> hits,
        (int Version, Dictionary<string, string> Stored, Dictionary<string, IntentRecord> Known) seen, CancellationToken ct)
    {
        foreach (var hit in hits)
        {
            if (seen.Stored.TryGetValue(hit.Ref.Canonical.Value, out var version) && string.Equals(version, hit.Ref.Version.Value, StringComparison.Ordinal))
            {
                _unchanged++;
                continue;
            }

            if (_fetched > 0 && OverCap())
            {
                return (true, null);
            }

            var fetched = await Within(t => source.Fetch(hit.Ref, t), ct).ConfigureAwait(false);
            if (fetched.IsError)
            {
                if (fetched.Error.Kind == ErrorKind.NotFound)
                {
                    continue;   // gone since the search
                }

                return (false, fetched.Error.Code);
            }

            var one = Linked(hit, fetched.Value, seen.Known);
            var committed = await ports.Store.CommitPage(new IntentPage(_id, [one.Record], one.Links, Cursor: null, ports.Clock.GetUtcNow()) { MapVersion = seen.Version }, ct).ConfigureAwait(false);
            if (committed.IsError)
            {
                return (false, committed.Error.Code);
            }

            await Decisions.Run([one], ct).ConfigureAwait(false);
        }

        return (false, null);
    }

    private PendingRecord Linked(SourceHit hit, SourceRecord fetched, Dictionary<string, IntentRecord> known)
    {
        var record = Record(hit, fetched);
        known[record.Key.Value] = record;
        var text = Text(fetched);
        var found = RulesLinker.Link(record.Key, text, map.Graph, [.. known.Values], new HashSet<string>(context.Settings.LinearTeams, StringComparer.Ordinal));
        _fetched++;
        _linked += found.Count;
        _unlinked += found.Count == 0 ? 1 : 0;
        return new PendingRecord(record, text, found);
    }

    private IntentRecord Record(SourceHit hit, SourceRecord fetched)
    {
        var title = context.Scrubbers.Ingest.Clean(fetched.Title, singleLine: true);
        var aliases = hit.Aliases.Select(a => context.Scrubbers.Ingest.Clean(a, singleLine: true)).Where(a => !a.Blocked).Select(a => a.Text).ToList();
        return new IntentRecord(hit.Ref.Canonical, _id, hit.Ref.Version.Value, title.Blocked ? "(withheld by the scrubber)" : title.Text, aliases) { CreatedAt = fetched.CreatedAt };
    }

    private string Text(SourceRecord fetched) => fetched.Blocked ? string.Empty : context.Scrubbers.Ingest.Clean(fetched.Text, singleLine: false) is { Blocked: false } clean ? clean.Text : string.Empty;

    private async ValueTask<Result<T>> Within<T>(Func<CancellationToken, ValueTask<Result<T>>> call, CancellationToken ct) =>
        await SourceCalls.Within(source, (context.Settings.Deadline, ports.Clock), call, ct).ConfigureAwait(false);

    private async ValueTask<SourceSync> Summary(string? code, CancellationToken ct)
    {
        var layer = await ports.Store.Layer(ct).ConfigureAwait(false);
        var state = await ports.Store.State(_id, ct).ConfigureAwait(false);
        var orphaned = layer.IsError ? 0 : IntentOverlay.Apply(map.Graph, layer.Value).Orphaned.Count;
        return Row(state.IsError ? 0 : state.Value.Records, code, orphaned, state.IsError ? (0, 0) : (state.Value.Decisions, state.Value.Queued));
    }

    private SourceSync Row(int records, string? code, int orphaned = 0, (int Decisions, int Queued) extraction = default) =>
        new(_id, records, _fetched, _unchanged, _linked, _unlinked, orphaned, extraction.Decisions, extraction.Queued, Decisions.Refused, Decisions.Tokens, code);
}
