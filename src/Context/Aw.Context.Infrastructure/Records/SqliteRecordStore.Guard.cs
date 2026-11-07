using System.Globalization;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Records;

internal sealed partial class SqliteRecordStore
{
    // why: Kernel.Data rethrows a SQLite code it does not map (a broken trigger's SQLITE_ERROR, LOCKED, INTERRUPT, TOOBIG) as a bug, but the callers of a write (the Linear write-through after a landed write)
    // read store failures as Results and cannot catch DbException (ARCH-PORT-001, #2172). The catch sits outside the unit of work's 'await using', so the transaction is rolled back before the error returns. A cancel is not a store failure: SQLite reports an interrupted statement as a SqliteException, so the catch stands aside once the token is cancelled and the cancellation surfaces as itself.
    private static async ValueTask<Result<T>> Guarded<T>(Func<ValueTask<Result<T>>> write, string what, CancellationToken cancellationToken)
    {
        try
        {
            return await write().ConfigureAwait(false);
        }
        catch (Microsoft.Data.Sqlite.SqliteException e) when (!cancellationToken.IsCancellationRequested)
        {
            return ContextErrors.StoreUnavailable.With($"{what}: SQLite {e.SqliteErrorCode.ToString(CultureInfo.InvariantCulture)} (extended {e.SqliteExtendedErrorCode.ToString(CultureInfo.InvariantCulture)}): {e.Message}{ContextErrors.StoreHint}");
        }
    }
}
