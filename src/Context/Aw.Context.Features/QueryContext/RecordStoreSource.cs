using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Search;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The record store's lexical leg as one more source of the query (spec §6.1): the leg's best chunk per record, ranked as the leg ordered them, scores dropped.
/// A hit's reference carries the stored record's version and link and its key as the canonical key, so it fuses with the other sources' hits of the same record.
/// A store that can't be read, or a leg that says it is degraded, is an unavailable source. The text it fetches is the stored, already scrubbed chunks.
/// </summary>
internal sealed class RecordStoreSource(ILexicalLeg leg, IRecordStore store, AudienceScope scope, TimeProvider clock) : IContextSource
{
    public static SourceId Id { get; } = SourceId.From("records");

    private static readonly SourceDescriptor _descriptor = new(Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false);

    public SourceDescriptor Describe() => _descriptor;

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var found = await leg.Search(query.Terms, scope, ILexicalLeg.DefaultLimit, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value.Degraded is { } degraded)
        {
            return ContextErrors.SourceUnavailable.With(degraded.Message);
        }

        return await RecordHits.Read(store, scope, clock, found.Value.Hits.Select(h => h.Chunk.Record), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var record = await store.Get(reference.Canonical, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        if (record.Value is not { RemovedAt: null } stored || !scope.Allows(stored.Audience, stored.Key))
        {
            return ContextErrors.UnknownAnchor.With("not a record of this store");   // gone, or never the caller's to read
        }

        if (stored.Blocked)
        {
            return new SourceRecord(reference, stored.Kind, stored.Title, string.Empty, stored.Author, stored.CreatedAt, stored.UpdatedAt) { Blocked = true };
        }

        var chunks = await store.Chunks(stored.Key, cancellationToken).ConfigureAwait(false);
        return chunks.IsError
            ? chunks.Error
            : new SourceRecord(reference, stored.Kind, stored.Title, string.Join('\n', chunks.Value.Select(c => c.Text)), stored.Author, stored.CreatedAt, stored.UpdatedAt);
    }

    public async ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var versions = new List<SourceVersion?>();
        foreach (var reference in refs)
        {
            var record = await store.Get(reference.Canonical, cancellationToken).ConfigureAwait(false);
            if (record.IsError)
            {
                return record.Error;
            }

            versions.Add(record.Value is { RemovedAt: null } stored && scope.Allows(stored.Audience, stored.Key) ? stored.Version : null);
        }

        return versions;
    }

    public async ValueTask<SourceHealth> Probe(CancellationToken cancellationToken)
    {
        var stamps = await store.Stamps(cancellationToken).ConfigureAwait(false);
        return stamps.IsError
            ? new SourceHealth(SourceState.Unavailable, "the record store can't be read", stamps.Error.Code)
            : new SourceHealth(SourceState.Ok, "the record store is readable", Code: null);
    }
}
