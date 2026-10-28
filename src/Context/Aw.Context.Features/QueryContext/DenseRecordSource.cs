using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The record store's dense leg as one more source of the query (spec §6.1): the query embedded with the pinned model, the chunk vectors scanned under the caller's
/// audience mask (inside the scan, never after it), the top 50 chunks cut by <see cref="DenseFloor"/>, the best chunk per record kept. It is its own source, so it fuses
/// by rank beside the lexical leg; its hits name the 'records' source, so one record found by both legs is one item and is fetched from <see cref="RecordStoreSource"/>.
/// A model that is down is an unavailable source with the spec's wording; no vectors yet is no hits and no line; partial coverage is a note.
/// </summary>
internal sealed class DenseRecordSource(RecordSearch search, AudienceScope scope, TimeProvider clock) : IContextSource, ISourceNotes
{
    public const int Chunks = 50;
    private const int PageSize = 1000;

    public static SourceId Id { get; } = SourceId.From("records-dense");

    private static readonly SourceDescriptor _descriptor = new(Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false);

    private List<string> _notes = [];

    public SourceDescriptor Describe() => _descriptor;

    public IReadOnlyList<string> TakeNotes()
    {
        var notes = _notes;
        _notes = [];
        return notes;
    }

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var embedded = await search.Embedder.Embed(string.Join(' ', query.Terms), cancellationToken).ConfigureAwait(false);
        if (embedded.IsError)
        {
            return embedded.Error.Code == ContextErrors.EmbedderUnavailable.Code
                ? ContextErrors.SourceUnavailable.With("meaning search is off: Ollama isn't running")
                : embedded.Error;
        }

        var matrix = await search.Matrix.Current(embedded.Value.Model, cancellationToken).ConfigureAwait(false);
        if (matrix.IsError)
        {
            return matrix.Error;
        }

        if (matrix.Value.Keys.Count == 0)
        {
            return Array.Empty<SourceHit>();   // nothing embedded yet: the lexical leg answers alone, and the empty store is not a failure
        }

        var records = await Nearest(matrix.Value, embedded.Value.Vec, cancellationToken).ConfigureAwait(false);
        return records.IsError
            ? records.Error
            : await RecordHits.Read(search.Store, scope, clock, records.Value, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SourceRecord>>(ContextErrors.UnknownAnchor.With("the dense leg fetches nothing; its hits are read from the records source"));

    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<SourceVersion?>>>(ContextErrors.UnknownAnchor.With("the dense leg checks no versions; its hits are checked by the records source"));

    public async ValueTask<SourceHealth> Probe(CancellationToken cancellationToken)
    {
        var stamps = await search.Store.Stamps(cancellationToken).ConfigureAwait(false);
        return stamps.IsError
            ? new SourceHealth(SourceState.Unavailable, "the record store can't be read", stamps.Error.Code)
            : new SourceHealth(SourceState.Ok, "the record store is readable", Code: null);
    }

    // The scan under the mask of records the caller may read, the floor, and the best chunk per record (hits come best first, so a record's first chunk is its best).
    private async ValueTask<Result<List<RecordKey>>> Nearest(VectorMatrix matrix, ReadOnlyMemory<float> query, CancellationToken cancellationToken)
    {
        var allowed = await Allowed(cancellationToken).ConfigureAwait(false);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        var mask = matrix.Keys.Select(k => ChunkKey.TryParse(k, out var chunk) && allowed.Value.Contains(chunk.Record)).ToArray();
        var top = search.Scan.Top(matrix, query, mask, Chunks);
        if (top.IsError)
        {
            return top.Error;
        }

        NoteCoverage(matrix, allowed.Value);
        return DenseFloor.Apply(top.Value)
            .Select(h => ChunkKey.TryParse(h.Key, out var chunk) ? chunk.Record : (RecordKey?)null)
            .OfType<RecordKey>()
            .Distinct()
            .ToList();
    }

    // The live, unblocked records the caller may read: the scan's mask is built from them, and they are the denominator of the coverage line.
    private async ValueTask<Result<HashSet<RecordKey>>> Allowed(CancellationToken cancellationToken)
    {
        var allowed = new HashSet<RecordKey>();
        RecordKey? after = null;
        while (true)
        {
            var page = await search.Store.List(new RecordFilter(), after, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var record in page.Value.Where(r => r.RemovedAt is null && !r.Blocked && scope.Allows(r.Audience, r.Key)))
            {
                _ = allowed.Add(record.Key);
            }

            if (page.Value.Count < PageSize)
            {
                return allowed;
            }

            after = page.Value[^1].Key;
        }
    }

    private void NoteCoverage(VectorMatrix matrix, HashSet<RecordKey> allowed)
    {
        var embedded = matrix.Keys.Select(k => ChunkKey.TryParse(k, out var chunk) ? chunk.Record : (RecordKey?)null).OfType<RecordKey>().Where(allowed.Contains).Distinct().Count();
        if (allowed.Count > 0 && embedded < allowed.Count)
        {
            var percent = (100L * embedded / allowed.Count).ToString(CultureInfo.InvariantCulture);
            _notes.Add($"meaning search covers {percent}% of your records; the rest are word-search only");
        }
    }
}
