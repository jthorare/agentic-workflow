using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The writer of <see cref="PrClaimSideRows"/> (spec 2026-11-04 §4.4): <c language="text">design_doc_claim</c>, migration 0031. Claims are refs only; they resolve on read.</summary>
internal sealed class PrClaimRowWriter : ISideRowWriter
{
    public string Id => PrClaimSideRows.WriterId;

    public async ValueTask<Result<bool>> Differs(ISqliteSession session, RecordKey key, SideRows rows, CancellationToken cancellationToken)
    {
        var stored = await SqliteQueries.List(session, new SqlStatement(DesignDocSql.StoredClaims, c => c.Bind("@key", key.Value)), SideRowReads.Text, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return ContextDatabase.Translate(stored.Error);
        }

        // why: a set, not a sequence: SQLite orders text by its UTF-8 bytes and string.CompareOrdinal by UTF-16 units, which differ for non-ASCII refs, so an ordered compare would rewrite them on every sync
        var wanted = ((PrClaimSideRows)rows).Refs.ToHashSet(StringComparer.Ordinal);
        return !wanted.SetEquals(stored.Value);
    }

    public async ValueTask<Error?> Replace(ISqliteSession session, RecordKey key, SideRows rows, CancellationToken cancellationToken)
    {
        var failure = await Delete(session, key, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        foreach (var claim in ((PrClaimSideRows)rows).Refs)
        {
            var (doc, item) = Halves(claim);
            failure = await SqliteRecordStore.Exec(session, DesignDocSql.InsertClaim, c => c.Bind("@key", key.Value).Bind("@ref", claim).Bind("@doc", doc).Bind("@item", item), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    public ValueTask<Error?> Delete(ISqliteSession session, RecordKey key, CancellationToken cancellationToken) =>
        SqliteRecordStore.Exec(session, DesignDocSql.DeleteClaims, c => c.Bind("@key", key.Value), cancellationToken);

    // 'harbor#REQ-1' or 'doc:aw/slug/harbor#REQ-1': the doc id is after the last '/' of the part before '#'. The parser only keeps refs with one '#'.
    private static (string Doc, string Item) Halves(string reference)
    {
        var hash = reference.LastIndexOf('#');
        var head = hash < 0 ? reference : reference[..hash];
        return (head[(head.LastIndexOf('/') + 1)..], hash < 0 ? string.Empty : reference[(hash + 1)..]);
    }
}
