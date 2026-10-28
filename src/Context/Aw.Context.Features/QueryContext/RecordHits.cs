using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// How a leg's records become source hits (spec §6.1): read back from the store, kept only while live and allowed, ranked in the order the leg gave them, scores dropped.
/// A hit's reference carries the stored record's version and link and its key as the canonical key, so the lexical and the dense leg's hits of one record fuse.
/// </summary>
internal static class RecordHits
{
    public static async ValueTask<Result<IReadOnlyList<SourceHit>>> Read(IRecordStore store, AudienceScope scope, TimeProvider clock, IEnumerable<RecordKey> keys, CancellationToken cancellationToken)
    {
        var hits = new List<SourceHit>();
        foreach (var key in keys)
        {
            var record = await store.Get(key, cancellationToken).ConfigureAwait(false);
            if (record.IsError)
            {
                return record.Error;
            }

            if (record.Value is { RemovedAt: null } stored && scope.Allows(stored.Audience, stored.Key) && Ref(stored) is { } reference)
            {
                hits.Add(new SourceHit(reference, stored.Title, hits.Count + 1, clock.GetUtcNow(), Leaky: false));
            }
        }

        return hits;
    }

    public static SourceRef? Ref(StoredRecord stored)
    {
        var link = Uri.TryCreate(stored.Url, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps ? url : null;
        return ExternalId.TryFrom(stored.Key.Value, out var id) ? new SourceRef(RecordStoreSource.Id, id, stored.Version, link, stored.Key) : null;
    }
}
