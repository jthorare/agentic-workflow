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

        var wanted = ((PrClaimSideRows)rows).Refs.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        return !stored.Value.SequenceEqual(wanted, StringComparer.Ordinal);
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
            failure = await SqliteRecordStore.Exec(session, DesignDocSql.InsertClaim, c => c.Bind("@key", key.Value).Bind("@ref", claim), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    public ValueTask<Error?> Delete(ISqliteSession session, RecordKey key, CancellationToken cancellationToken) =>
        SqliteRecordStore.Exec(session, DesignDocSql.DeleteClaims, c => c.Bind("@key", key.Value), cancellationToken);
}
