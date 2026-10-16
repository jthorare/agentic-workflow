using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>Opens a database for reading only. Read-only is enforced at the connection, not by parsing statements.</summary>
public interface IDatabaseReader
{
    /// <summary>Opens <paramref name="target"/>.</summary>
    /// <param name="target">The database.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>
    /// A read-only session, AW-EVIDENCE-024 when the database cannot be reached or copied, AW-EVIDENCE-021 when a server is not the one the
    /// entry names, or AW-EVIDENCE-020 when the role could write; no script query has run in any of those cases.
    /// </returns>
    public ValueTask<Result<IReadSession>> OpenAsync(DbTarget target, CancellationToken cancellationToken);
}
