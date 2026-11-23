using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Doctor;

/// <summary>
/// The doctor's reads of its stores on a home another build may have written (first-run spec §3.4, #2954): the views store's migration count, and a guard that turns
/// a SQLite failure no store maps into an error naming the store, so one bad file is a fail row and never a crash.
/// </summary>
public interface IDoctorStores
{
    /// <summary>The views store's applied migrations against this build's; a missing file, or one with no migrations yet, has none applied.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The status, or why it can't be read.</returns>
    public ValueTask<Result<SchemaStatus>> ViewsSchema(CancellationToken cancellationToken);

    /// <summary>Runs one read of <paramref name="file"/>; a SQLite failure inside it becomes an error whose message starts with the file's name.</summary>
    /// <typeparam name="T">The read's value.</typeparam>
    /// <param name="file">The store's file name, as the row shows it (ledger.db, views.db).</param>
    /// <param name="read">The read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The read's result, or the error naming the file.</returns>
    public ValueTask<Result<T>> Guard<T>(string file, Func<CancellationToken, ValueTask<Result<T>>> read, CancellationToken cancellationToken);
}
