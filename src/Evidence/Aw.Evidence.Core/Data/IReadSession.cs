using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>An open read-only database connection.</summary>
public interface IReadSession : IAsyncDisposable
{
    /// <summary>Runs one statement and reads at most <paramref name="rowLimit"/> rows.</summary>
    /// <param name="sql">Exactly one statement.</param>
    /// <param name="rowLimit">The most rows to keep.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>
    /// The rows; AW-EVIDENCE-020 when the connection refuses the statement (a write, a second statement, a bypass of read-only), or
    /// AW-EVIDENCE-015 when it fails for another reason (no such table, a connection lost). A refused statement leaves the session usable.
    /// </returns>
    public ValueTask<Result<Rows>> QueryAsync(string sql, int rowLimit, CancellationToken cancellationToken);
}
