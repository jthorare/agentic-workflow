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
        cancellationToken.ThrowIfCancellationRequested();   // a real store's transaction does not start under a cancelled token
        lock (_gate)
        {
            if (Validate(batch) is { } invalid)
            {
                return Fail<CommitReport>(invalid);
            }

            var (written, unchanged) = Apply(batch.Records, batch.Guard);
            var tombstoned = Tombstone(batch.Removed, batch.At);

            if (batch.Position is { } position)
            {
                _positions[batch.Source] = position;
            }

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
    public ValueTask<Result<LiveRecordCounts>> LiveCounts(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var live = _records.Values.Where(r => !r.Blocked && r.RemovedAt is null).ToList();
            var embedded = new HashSet<RecordKey>();
            foreach (var vector in _vectors.Where(v => v.Vector.Space == VectorSpace.Chunk))
            {
                if (ChunkKey.TryParse(vector.Vector.Key, out var chunk))
                {
                    _ = embedded.Add(chunk.Record);
                }
            }

            return Ok(new LiveRecordCounts(live.Count, live.Count(r => embedded.Contains(r.Key))));
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
    public ValueTask<Result<IReadOnlyList<ProjectRow>>> ProjectFacts(SourceId source, RecordAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        lock (_gate)
        {
            var rows = _records.Values
                .Where(r => r.Source == source && r.Kind == RecordKind.Project && r.RemovedAt is null && !r.Blocked && audience.Allows(r.Audience, r.Key))
                .Select(project => ProjectRowOf(project, audience))
                .OrderBy(r => r.Title, StringComparer.Ordinal).ThenBy(r => r.Key.Value, StringComparer.Ordinal);
            return Ok<IReadOnlyList<ProjectRow>>([.. rows]);
        }
    }

    private ProjectRow ProjectRowOf(StoredRecord project, RecordAudience audience)
    {
        var lead = project.Links.Where(l => l.Kind == RecordLinkKind.LedBy).OrderBy(l => l.ToRef, StringComparer.Ordinal).FirstOrDefault();
        var issues = _records.Values
            .Where(i => i.Kind == RecordKind.Issue && i.RemovedAt is null && audience.Allows(i.Audience, i.Key) && i.Links.Any(l => l.Kind == RecordLinkKind.InProject && l.ToRef == project.Key.Value))
            .ToList();
        var prs = PrsOf(issues, audience);
        return new ProjectRow(project.Key, project.Title, project.Url, project.StateRaw, project.Health, project.DueAt, project.UpdatedAt ?? project.SyncedAt, project.ClosedAt,
            lead?.ToRef, lead?.Title, issues.Count(i => i.State == RecordState.Open), issues.Max(i => i.UpdatedAt), project.Description,
            prs.Count(static p => p.State is RecordState.Open or RecordState.Draft),
            [.. prs.Select(static p => p.Key.Value["github:pr/".Length..p.Key.Value.LastIndexOf('/')]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            prs.Count == 0 ? null : prs.Max(static p => p.UpdatedAt ?? p.SyncedAt));
    }

    // A PR an issue links to, or one that names an issue's 'known-as' key; live, not withheld, visible, each once.
    private List<StoredRecord> PrsOf(List<StoredRecord> issues, RecordAudience audience)
    {
        var attached = issues.SelectMany(static i => i.Links.Where(static l => l.Kind == RecordLinkKind.Relates).Select(static l => l.ToRef)).ToHashSet(StringComparer.Ordinal);
        var names = issues.SelectMany(static i => i.Links.Where(static l => l.Kind == RecordLinkKind.KnownAs).Select(static l => l.ToRef)).ToHashSet(StringComparer.Ordinal);
        return [.. _records.Values.Where(p => p.Kind == RecordKind.Pr && p.RemovedAt is null && !p.Blocked && audience.Allows(p.Audience, p.Key)
            && (attached.Contains(p.Key.Value) || p.Links.Any(l => l.Kind == RecordLinkKind.Relates && names.Contains(l.ToRef))))];
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

    private (int Written, int Unchanged) Apply(IReadOnlyList<RecordWrite> writes, bool guard)
    {
        var written = 0;
        var unchanged = 0;
        foreach (var write in writes)
        {
            if (guard && Stale(write.Record))
            {
                unchanged++;   // a newer write is stored
                continue;
            }

            if (Same(write.Record))
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

    private bool Same(StoredRecord incoming) =>
        _records.TryGetValue(incoming.Key, out var existing) && existing.RemovedAt is null && existing.Version == incoming.Version && existing.TextHash == incoming.TextHash
        && existing.Ruleset == incoming.Ruleset && existing.Blocked == incoming.Blocked;

    // The stored record is strictly newer than the incoming one; an equal time passes, and so does a record with no time on either side.
    private bool Stale(StoredRecord incoming) =>
        _records.TryGetValue(incoming.Key, out var kept) && kept.UpdatedAt is { } stored && incoming.UpdatedAt is { } at && at < stored;

    private int Tombstone(IReadOnlyList<RecordKey> removed, DateTimeOffset at)
    {
        var tombstoned = 0;
        foreach (var key in removed)
        {
            if (_records.TryGetValue(key, out var row) && row.RemovedAt is null)
            {
                tombstoned++;
                DropChunkVectors((_chunks.GetValueOrDefault(key) ?? []).Select(c => new ChunkKey(key, c.Seq).ToString()));
                _records[key] = row with { RemovedAt = at, Description = null };
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
