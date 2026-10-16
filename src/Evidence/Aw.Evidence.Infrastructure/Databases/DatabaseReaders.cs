using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>Opens each target with the reader for its engine.</summary>
internal sealed class DatabaseReaders(SqliteDatabaseReader sqlite, PostgresDatabaseReader postgres) : IDatabaseReader
{
    /// <inheritdoc/>
    public ValueTask<Result<IReadSession>> OpenAsync(DbTarget target, CancellationToken cancellationToken) => target switch
    {
        SqliteTarget => sqlite.OpenAsync(target, cancellationToken),
        PostgresTarget => postgres.OpenAsync(target, cancellationToken),
        _ => ValueTask.FromResult<Result<IReadSession>>(EvidenceErrors.DatabaseRefused.With("no reader for this kind of database")),
    };
}
