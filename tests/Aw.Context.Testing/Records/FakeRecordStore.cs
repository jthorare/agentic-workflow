using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>The in-memory <see cref="IRecordStore"/>: dictionaries behind one lock, a batch validated whole before any of it is applied. It passes <see cref="RecordStoreContract{TStore}"/>.</summary>
public sealed class FakeRecordStore : IRecordStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<RecordKey, StoredRecord> _records = [];
    private readonly Dictionary<RecordKey, List<RecordChunk>> _chunks = [];
    private readonly Dictionary<SourceId, SyncPosition> _positions = [];
    private readonly Dictionary<SourceId, SyncOutcome> _outcomes = [];
    private readonly Dictionary<SourceId, SourceBackoffState> _backoffs = [];
    private readonly List<VectorRow> _vectors = [];
    private long _seq;
    private long _recordStamp;
    private long _deleteEpoch;

    /// <inheritdoc />
    public ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        lock (_gate)
        {
            if (Validate(batch) is { } invalid)
            {
                return Fail<CommitReport>(invalid);
            }

            var (written, unchanged) = Apply(batch.Records);
            var tombstoned = Tombstone(batch.Removed, batch.At);

            _positions[batch.Source] = batch.Position;
            if (written + tombstoned > 0)
            {
                _recordStamp++;
            }

            return Ok(new CommitReport(written, unchanged, tombstoned));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok(_records.TryGetValue(key, out var row) ? Resolve(row) : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok<IReadOnlyList<RecordChunk>>([.. (_chunks.GetValueOrDefault(key) ?? []).OrderBy(c => c.Seq)]);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (limit is < 1 or > 1000)
        {
            return Fail<IReadOnlyList<StoredRecord>>(ContextErrors.QueryInvalid.With("a listing returns 1 to 1,000 records"));
        }

        lock (_gate)
        {
            var page = _records.Values
                .Where(r => Matches(filter, r) && (after is not { } last || string.CompareOrdinal(r.Key.Value, last.Value) > 0))
                .OrderBy(r => r.Key.Value, StringComparer.Ordinal)
                .Take(limit)
                .Select(Resolve)
                .ToList();
            return Ok<IReadOnlyList<StoredRecord>>(page);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok(_positions.GetValueOrDefault(source) ?? SyncPosition.Start);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> RecordOutcome(SourceId source, SyncOutcome outcome, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _outcomes[source] = outcome;
            return Ok(1);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<SyncOutcome?>> LastOutcome(SourceId source, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok(_outcomes.GetValueOrDefault(source));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> RecordAttempt(SourceId source, SyncAttempt attempt, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _backoffs[source] = SourceBackoff.Apply(_backoffs.GetValueOrDefault(source, SourceBackoffState.None), attempt);
            return Ok(1);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<SourceBackoffState>> Backoff(SourceId source, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok(_backoffs.GetValueOrDefault(source, SourceBackoffState.None));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        lock (_gate)
        {
            var stored = 0;
            foreach (var vector in vectors)
            {
                if (vector.Space == VectorSpace.Chunk && !ChunkHasHash(vector))
                {
                    continue;
                }

                _deleteEpoch += _vectors.RemoveAll(v => v.Vector.Space == vector.Space && v.Vector.Key == vector.Key && v.Vector.Model == vector.Model);
                _vectors.Add(new VectorRow(++_seq, vector));
                stored++;
            }

            if (stored > 0)
            {
                _recordStamp++;
            }

            return Ok(stored);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 10_000)
        {
            return Fail<IReadOnlyList<VectorRow>>(ContextErrors.QueryInvalid.With("a vector read returns 1 to 10,000 rows"));
        }

        lock (_gate)
        {
            return Ok<IReadOnlyList<VectorRow>>([.. _vectors.Where(v => v.Vector.Space == space && v.Seq > afterSeq).OrderBy(v => v.Seq).Take(limit)]);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<ChunkToEmbed>>> ChunksToEmbed(string model, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(model);
        if (limit is < 1 or > 10_000)
        {
            return Fail<IReadOnlyList<ChunkToEmbed>>(ContextErrors.QueryInvalid.With("a work list returns 1 to 10,000 chunks"));
        }

        lock (_gate)
        {
            var embedded = _vectors
                .Where(v => v.Vector.Space == VectorSpace.Chunk && v.Vector.Model == model)
                .Select(v => (v.Vector.Key, v.Vector.TextHash))
                .ToHashSet();
            var work = _records.Values
                .Where(r => !r.Blocked && r.RemovedAt is null)
                .OrderBy(r => r.UpdatedAt is null)
                .ThenByDescending(r => r.UpdatedAt)
                .ThenBy(r => r.Key.Value, StringComparer.Ordinal)
                .SelectMany(r => (_chunks.GetValueOrDefault(r.Key) ?? []).OrderBy(c => c.Seq).Select(c => new ChunkToEmbed(new ChunkKey(r.Key, c.Seq).ToString(), c.Text, c.TextHash)))
                .Where(c => !embedded.Contains((c.Key, c.TextHash)))
                .Take(limit)
                .ToList();
            return Ok<IReadOnlyList<ChunkToEmbed>>(work);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (_gate)
        {
            var removed = _vectors.RemoveAll(v => v.Vector.Space == space && keys.Contains(v.Vector.Key, StringComparer.Ordinal));
            _deleteEpoch += removed;
            return Ok(removed);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok(new StoreStamps(_recordStamp, _deleteEpoch));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<SourceCounts>> Count(SourceId source, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var keys = _records.Values.Where(r => r.Source == source).Select(r => r.Key).ToList();
            var chunks = keys.SelectMany(k => (_chunks.GetValueOrDefault(k) ?? []).Select(c => new ChunkKey(k, c.Seq).ToString())).ToHashSet(StringComparer.Ordinal);
            return Ok(new SourceCounts(keys.Count, chunks.Count, _vectors.Count(v => v.Vector.Space == VectorSpace.Chunk && chunks.Contains(v.Vector.Key))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var keys = _records.Values.Where(r => r.Source == source).Select(r => r.Key).ToList();
            DropChunkVectors(keys.SelectMany(k => (_chunks.GetValueOrDefault(k) ?? []).Select(c => new ChunkKey(k, c.Seq).ToString())));
            foreach (var key in keys)
            {
                _ = _records.Remove(key);
                _ = _chunks.Remove(key);
            }

            _ = _positions.Remove(source);
            _ = _outcomes.Remove(source);
            return Ok(keys.Count);
        }
    }

    private (int Written, int Unchanged) Apply(IReadOnlyList<RecordWrite> writes)
    {
        var written = 0;
        var unchanged = 0;
        foreach (var write in writes)
        {
            if (_records.TryGetValue(write.Record.Key, out var existing) && existing.RemovedAt is null && existing.Version == write.Record.Version && existing.TextHash == write.Record.TextHash
                && existing.Ruleset == write.Record.Ruleset && existing.Blocked == write.Record.Blocked)
            {
                unchanged++;
                continue;
            }

            written++;
            var old = _chunks.GetValueOrDefault(write.Record.Key) ?? [];
            DropChunkVectors(old.Where(o => write.Chunks.All(n => n.Seq != o.Seq || n.TextHash != o.TextHash)).Select(o => new ChunkKey(write.Record.Key, o.Seq).ToString()));
            _records[write.Record.Key] = write.Record with { RemovedAt = null };
            _chunks[write.Record.Key] = [.. write.Chunks];
        }

        return (written, unchanged);
    }

    private int Tombstone(IReadOnlyList<RecordKey> removed, DateTimeOffset at)
    {
        var tombstoned = 0;
        foreach (var key in removed)
        {
            if (_records.TryGetValue(key, out var row) && row.RemovedAt is null)
            {
                tombstoned++;
                DropChunkVectors((_chunks.GetValueOrDefault(key) ?? []).Select(c => new ChunkKey(key, c.Seq).ToString()));
                _records[key] = row with { RemovedAt = at };
                _ = _chunks.Remove(key);
            }
        }

        return tombstoned;
    }

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private static ValueTask<Result<T>> Fail<T>(Error error) => ValueTask.FromResult<Result<T>>(error);

    private static bool Matches(RecordFilter filter, StoredRecord r) =>
        (filter.IncludeRemoved || r.RemovedAt is null) && (filter.Source is not { } s || r.Source == s) && (filter.Repo is not { } repo || r.Repo == repo) && (filter.Kind is not { } kind || r.Kind == kind);

    private static Error? Validate(RecordBatch batch)
    {
        foreach (var write in batch.Records)
        {
            if (write.Record.Source != batch.Source)
            {
                return ContextErrors.RecordBatchInvalid.With($"record '{write.Record.Key.Value}' belongs to another source than the batch");
            }

            if (write.Record.Blocked && write.Chunks.Count > 0)
            {
                return ContextErrors.RecordBatchInvalid.With($"blocked record '{write.Record.Key.Value}' has chunks");
            }

            if (write.Chunks.Select(c => c.Seq).Order().Select((seq, index) => seq == index).Contains(false))
            {
                return ContextErrors.RecordBatchInvalid.With($"the chunks of '{write.Record.Key.Value}' do not number 0 up without gaps");
            }
        }

        return null;
    }

    private StoredRecord Resolve(StoredRecord row) =>
        row.Links.Count == 0
            ? row
            : row with { Links = [.. row.Links.Select(l => RecordKey.TryFrom(l.ToRef, out var to) && _records.ContainsKey(to) ? l with { ToKey = to } : l)] };

    private bool ChunkHasHash(VectorWrite vector) =>
        ChunkKey.TryParse(vector.Key, out var key)
        && _records.TryGetValue(key.Record, out var row) && row.RemovedAt is null
        && _chunks.GetValueOrDefault(key.Record)?.Exists(c => c.Seq == key.Seq && c.TextHash == vector.TextHash) == true;

    private void DropChunkVectors(IEnumerable<string> chunkKeys)
    {
        var keys = chunkKeys.ToHashSet(StringComparer.Ordinal);
        _deleteEpoch += _vectors.RemoveAll(v => v.Vector.Space == VectorSpace.Chunk && keys.Contains(v.Vector.Key));
    }
}
