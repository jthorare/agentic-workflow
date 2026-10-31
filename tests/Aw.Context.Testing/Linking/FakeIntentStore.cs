using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Linking;

/// <summary>The in-memory intent store; <see cref="IntentStoreContract"/> holds it to the SQLite store's behavior.</summary>
public sealed class FakeIntentStore : IIntentStore
{
    private readonly Dictionary<string, IntentRecord> _records = new(StringComparer.Ordinal);
    private IntentLayer? _layer;
    private readonly Dictionary<string, List<IntentLink>> _links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string? Cursor, DateTimeOffset? LastSync, string? LastError)> _cursors = new(StringComparer.Ordinal);

    /// <summary>When set, every call fails with it (a store that can't open).</summary>
    private readonly Dictionary<string, string> _extracted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _linkedMap = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DecisionRow>> _decisions = new(StringComparer.Ordinal);
    private readonly List<GraphEdge> _edges = [];
    private readonly Dictionary<(string, string), DecisionConflict> _conflicts = [];

    public Error? Failure { get; set; }

    /// <summary>When set, <see cref="Requeue"/> alone fails with it.</summary>
    public Error? RequeueFailure { get; set; }

    /// <summary>When set, <see cref="State"/> alone fails with it.</summary>
    public Error? StateFailure { get; set; }

    /// <summary>When set, <see cref="LinkedUnder"/> alone fails with it.</summary>
    public Error? LinkedUnderFailure { get; set; }

    /// <summary>When set, <see cref="Queued"/> alone fails with it.</summary>
    public Error? QueuedFailure { get; set; }

    /// <summary>When set, <see cref="SaveConflict"/> alone fails with it.</summary>
    public Error? SaveConflictFailure { get; set; }

    /// <summary>When set, the <see cref="Layer"/> call with this number (1 is the first) alone fails with <see cref="LayerFailure"/>.</summary>
    public int? LayerFailureOnCall { get; set; }

    /// <summary>The error the numbered <see cref="Layer"/> call fails with.</summary>
    public Error? LayerFailure { get; set; }

    private int _layerCalls;

    /// <summary>When set, a <see cref="SaveDecisions"/> of a batch with no decisions alone fails with it (the pass's refusal of an oversized record).</summary>
    public Error? EmptyBatchFailure { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<IntentLayer>> Layer(CancellationToken cancellationToken)
    {
        if (++_layerCalls == LayerFailureOnCall && LayerFailure is { } failure)
        {
            return ValueTask.FromResult<Result<IntentLayer>>(failure);
        }

        return Done(() => _layer ??= new IntentLayer(
            [.. _records.Values.OrderBy(r => r.Key.Value, StringComparer.Ordinal)],
            [.. _links.Values.SelectMany(l => l).OrderBy(l => l.Record.Value, StringComparer.Ordinal).ThenBy(l => l.Target.Value, StringComparer.Ordinal).ThenBy(l => l.Rule)],
            [.. _decisions.Values.SelectMany(d => d).OrderBy(d => d.ToKey().Value, StringComparer.Ordinal)],
            [.. _edges.OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => e.Type)])
        {
            Conflicts = [.. _conflicts.Values.OrderBy(c => c.A.Value, StringComparer.Ordinal).ThenBy(c => c.B.Value, StringComparer.Ordinal)],
        });
    }

    /// <inheritdoc/>
    public ValueTask<Result<IntentSourceState>> State(SourceId source, CancellationToken cancellationToken) => StateFailure is { } stateFailure
        ? ValueTask.FromResult<Result<IntentSourceState>>(stateFailure)
        : Done(() =>
    {
        _ = _cursors.TryGetValue(source.Value, out var cursor);
        var mine = _records.Values.Where(r => r.Source.Value == source.Value).Select(r => r.Key.Value).ToHashSet(StringComparer.Ordinal);
        return new IntentSourceState(cursor.Cursor, cursor.LastSync, cursor.LastError, mine.Count, _links.Where(l => mine.Contains(l.Key)).Sum(l => l.Value.Count),
            _decisions.Where(d => mine.Contains(d.Key)).Sum(d => d.Value.Count), mine.Count(k => IsQueued(k)));
    });

    /// <inheritdoc/>
    public ValueTask<Result<IntentCommit>> CommitPage(IntentPage page, CancellationToken cancellationToken)
    {
        _layer = null;   // the store keeps its layer until a write, as SQLite keeps it until the change stamp moves
        ArgumentNullException.ThrowIfNull(page);
        return Attempt(() =>
        {
            var known = page.Records.Select(r => r.Key.Value).Concat(_records.Keys).ToHashSet(StringComparer.Ordinal);
            if (page.Links.FirstOrDefault(l => !known.Contains(l.Record.Value)) is { } orphan)
            {
                return ContextErrors.StoreUnavailable.With("a link names a record that isn't stored: " + orphan.Record.Value);
            }

            var changed = 0;
            foreach (var record in page.Records)
            {
                var isNew = !_records.TryGetValue(record.Key.Value, out var stored) || stored.Version != record.Version;
                changed += isNew ? 1 : 0;
                _records[record.Key.Value] = isNew ? record with { FetchedAt = page.At } : stored!;
                if (page.MapVersion > 0)
                {
                    if (_linkedMap.GetValueOrDefault(record.Key.Value) != page.MapVersion)
                    {
                        _ = _extracted.Remove(record.Key.Value);   // SQLite: queued in the same statement as the move
                    }

                    _linkedMap[record.Key.Value] = page.MapVersion;
                }

                _links[record.Key.Value] = [.. page.Links.Where(l => l.Record.Value == record.Key.Value).DistinctBy(l => (l.Target.Value, l.Rule))];
            }

            _ = _cursors.TryGetValue(page.Source.Value, out var before);
            _cursors[page.Source.Value] = (page.Cursor ?? before.Cursor, page.At, null);
            return Results.Success(new IntentCommit(page.Records.Count, page.Links.Count, changed));
        });
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> RecordFailure(SourceId source, string code, CancellationToken cancellationToken) => Done(() =>
    {
        _ = _cursors.TryGetValue(source.Value, out var before);
        _cursors[source.Value] = (before.Cursor, before.LastSync, code);
        return Unit.Value;
    });

    public ValueTask<Result<IReadOnlyList<RecordKey>>> Queued(SourceId source, CancellationToken cancellationToken) => QueuedFailure is { } queuedFailure
        ? ValueTask.FromResult<Result<IReadOnlyList<RecordKey>>>(queuedFailure)
        : Done<IReadOnlyList<RecordKey>>(() =>
        [.. _records.Values.Where(r => r.Source.Value == source.Value && IsQueued(r.Key.Value)).OrderBy(r => r.Key.Value, StringComparer.Ordinal).Select(r => r.Key)]);

    public ValueTask<Result<IReadOnlyList<RecordKey>>> LinkedUnder(SourceId source, int mapVersion, CancellationToken cancellationToken) => LinkedUnderFailure is { } failure
        ? ValueTask.FromResult<Result<IReadOnlyList<RecordKey>>>(failure)
        : Done<IReadOnlyList<RecordKey>>(() =>
        [.. _records.Values.Where(r => r.Source.Value == source.Value && _linkedMap.GetValueOrDefault(r.Key.Value) == mapVersion).OrderBy(r => r.Key.Value, StringComparer.Ordinal).Select(r => r.Key)]);

    public ValueTask<Result<Unit>> SaveDecisions(DecisionBatch batch, CancellationToken cancellationToken)
    {
        _layer = null;   // the store keeps its layer until a write, as SQLite keeps it until the change stamp moves
        ArgumentNullException.ThrowIfNull(batch);
        if (EmptyBatchFailure is { } failure && batch.Decisions.Count == 0)
        {
            return ValueTask.FromResult<Result<Unit>>(failure);
        }

        return Done(() =>
        {
            if (!_records.TryGetValue(batch.Record.Value, out var stored) || stored.Version != batch.Version)
            {
                return Unit.Value;
            }

            var current = _decisions.GetValueOrDefault(batch.Record.Value, []);
            var keys = batch.Decisions.Select(d => d.ToKey().Value).ToHashSet(StringComparer.Ordinal);
            var machine = current.Where(d => d.Provenance != Provenance.Human).Select(d => d.ToKey().Value).ToHashSet(StringComparer.Ordinal);
            var removed = machine.Where(k => !keys.Contains(k)).ToHashSet(StringComparer.Ordinal);   // a surviving decision keeps its verdicts; a human one is never replaced
            _ = _edges.RemoveAll(e => machine.Contains(e.From.Value));
            foreach (var gone in _conflicts.Keys.Where(k => removed.Contains(k.Item1) || removed.Contains(k.Item2)).ToList())
            {
                _ = _conflicts.Remove(gone);
            }

            var human = current.Where(d => d.Provenance == Provenance.Human).ToList();
            var humanKeys = human.Select(d => d.ToKey().Value).ToHashSet(StringComparer.Ordinal);
            _decisions[batch.Record.Value] = [.. human, .. batch.Decisions.Where(d => !humanKeys.Contains(d.ToKey().Value))];   // SQLite: a human row is never replaced
            _edges.AddRange(batch.Edges.Where(e => !_edges.Exists(x => x.From == e.From && x.To == e.To && x.Type == e.Type)));   // SQLite: ON CONFLICT DO NOTHING
            if (batch.Pending)
            {
                _ = _extracted.Remove(batch.Record.Value);   // queued again, a done record included
            }
            else
            {
                _extracted[batch.Record.Value] = batch.Version;
            }

            return Unit.Value;
        });
    }

    public ValueTask<Result<Unit>> Requeue(RecordKey record, string version, CancellationToken cancellationToken)
    {
        _layer = null;   // the store keeps its layer until a write, as SQLite keeps it until the change stamp moves
        if (RequeueFailure is { } failure)
        {
            return ValueTask.FromResult<Result<Unit>>(failure);
        }

        return Done(() =>
        {
            if (_records.TryGetValue(record.Value, out var stored) && stored.Version == version)
            {
                _ = _extracted.Remove(record.Value);
            }

            return Unit.Value;
        });
    }

    public ValueTask<Result<Unit>> ReplaceRuleLinks(RecordKey record, LinkRule rule, IReadOnlyList<IntentLink> links, CancellationToken cancellationToken)
    {
        _layer = null;   // the store keeps its layer until a write, as SQLite keeps it until the change stamp moves
        return Done(() =>
        {
            if (_records.ContainsKey(record.Value))   // SQLite: a record that isn't stored takes no link
            {
                var kept = _links.GetValueOrDefault(record.Value)?.Where(l => l.Rule != rule) ?? [];
                _links[record.Value] = [.. kept, .. links.Where(l => l.Record.Value == record.Value).Select(l => l with { Rule = rule }).DistinctBy(l => (l.Target.Value, l.Rule))];
            }

            return Unit.Value;
        });
    }

    public ValueTask<Result<Unit>> SetAliases(RecordKey record, string version, IReadOnlyList<string> aliases, CancellationToken cancellationToken)
    {
        _layer = null;   // the store keeps its layer until a write, as SQLite keeps it until the change stamp moves
        return Done(() =>
        {
            if (_records.TryGetValue(record.Value, out var stored) && stored.Version == version)
            {
                _records[record.Value] = stored with { Aliases = [.. aliases] };
            }

            return Unit.Value;
        });
    }

    public ValueTask<Result<Unit>> SaveConflict(DecisionConflict conflict, CancellationToken cancellationToken)
    {
        _layer = null;   // the store keeps its layer until a write, as SQLite keeps it until the change stamp moves
        ArgumentNullException.ThrowIfNull(conflict);
        if (SaveConflictFailure is { } failure)
        {
            return ValueTask.FromResult<Result<Unit>>(failure);
        }

        return Done(() =>
        {
            var pair = (conflict.A.Value, conflict.B.Value);
            if (!_conflicts.TryGetValue(pair, out var stored) || stored.Verdict == ConflictVerdict.Unread)
            {
                _conflicts[pair] = conflict;   // SQLite: stored once; only an unread row is overwritten
            }
            return Unit.Value;
        });
    }

    private bool IsQueued(string key) => !_extracted.TryGetValue(key, out var version) || version != _records[key].Version;

    private ValueTask<Result<T>> Done<T>(Func<T> work) => Attempt(() => Results.Success(work()));

    private ValueTask<Result<T>> Attempt<T>(Func<Result<T>> work) => ValueTask.FromResult(Failure is { } failure ? failure : work());
}
