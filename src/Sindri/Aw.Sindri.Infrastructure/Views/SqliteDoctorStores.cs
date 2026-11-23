using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ledger;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The doctor's store reads (first-run spec §3.4, #2954): views.db's migration count, read-only and never creating the file, and the guard that turns a
/// SQLite failure the stores leave as a throw (SQLITE_ERROR: a table or column an older build didn't make) into an error naming the file.
/// </summary>
internal sealed class SqliteDoctorStores(ViewsDatabase views) : IDoctorStores
{
    private const string ViewsFile = "views.db";

    public async ValueTask<Result<SchemaStatus>> ViewsSchema(CancellationToken cancellationToken)
    {
        var expected = ViewsDatabase.Migrations.Count;
        if (!views.Exists)
        {
            return new SchemaStatus(0, expected);
        }

        var read = await views.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ViewsDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var present = await SqliteQueries.Exists(read.Value, new SqlStatement(ViewSql.TableExists, c => c.Bind("$name", ViewSql.VersionTable)), cancellationToken).ConfigureAwait(false);
            if (present.IsError || !present.Value)
            {
                return present.IsError ? ViewsDatabase.Translate(present.Error) : new SchemaStatus(0, expected);
            }

            var applied = await SqliteQueries.One(read.Value, new SqlStatement(ViewSql.CountApplied), AppliedMapper.Instance, cancellationToken).ConfigureAwait(false);
            return applied.IsError ? ViewsDatabase.Translate(applied.Error) : new SchemaStatus(applied.Value, expected);
        }
    }

    public async ValueTask<Result<T>> Guard<T>(string file, Func<CancellationToken, ValueTask<Result<T>>> read, CancellationToken cancellationToken)
    {
        try
        {
            return await read(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)   // boundary: doctor runs on a broken home, so a store's unmapped SQLite failure is its row, never a crash (#2954)
        {
            var definition = string.Equals(file, ViewsFile, StringComparison.Ordinal) ? SindriErrors.ViewsStalled : SindriErrors.LedgerUnavailable;
            return definition.With($"{file} can't be read: {exception.Message}");
        }
    }

    /// <summary>Maps the one count column.</summary>
    private sealed class AppliedMapper : IRowMapper<int>
    {
        public static readonly AppliedMapper Instance = new();

        public int Map(SqliteDataReader reader) => reader.GetInt32(0);
    }
}
