using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>
/// #1280: a TS judge ran on the C# database after the cutover's migrations 1-3 and left its own eval_items and labels
/// tables (and the eval_items_question index), so migration 0004's plain CREATE TABLE failed with "table eval_items
/// already exists". While 0004 isn't applied, each of those tables that exists is renamed to ts_eval_items / ts_labels
/// in one transaction, rows kept, and the TS index is dropped. A ts_* table that already exists is never overwritten:
/// that table is left where it is, and the migration then reports the clash. A new database, or one at schema 4 or
/// later, is not touched, and a file that isn't a judge database is only read.
/// </summary>
internal static class TsEvalTables
{
    private static readonly string[] _present = ["eval_items", "labels"];
    private const string ListTables = "SELECT name FROM sqlite_master WHERE type = 'table'";
    private const string AppliedFour = "SELECT COUNT(*) FROM schema_version WHERE version >= 4";
    private const string RenameItems = "ALTER TABLE eval_items RENAME TO ts_eval_items";
    private const string RenameLabels = "ALTER TABLE labels RENAME TO ts_labels";
    private const string DropIndex = "DROP INDEX IF EXISTS eval_items_question";

    internal static async ValueTask<Result<Unit>> Park(SqliteConnectionFactory connections, string databasePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(databasePath))
        {
            return Results.Ok();
        }

        try
        {
            var names = await TablesToPark(connections, cancellationToken).ConfigureAwait(false);
            return names.IsError ? names.Error : names.Value.Length == 0 ? Results.Ok() : await Rename(connections, names.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            return DataErrors.Unavailable.With($"Can't set the TS judge's eval tables aside in '{databasePath}' (#1280): {exception.Message}");
        }
    }

    // The TS-made tables to rename: only while schema 4 isn't applied, only those present, never onto an existing ts_* table.
    private static async ValueTask<Result<string[]>> TablesToPark(SqliteConnectionFactory connections, CancellationToken cancellationToken)
    {
        var opened = await connections.OpenReadOnly(cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        var connection = opened.Value;
        await using (connection.ConfigureAwait(false))
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var list = connection.CreateCommand();
            await using (list.ConfigureAwait(false))
            {
                list.CommandText = ListTables;
                var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        _ = names.Add(reader.GetString(0));
                    }
                }
            }

            if (!names.Contains("schema_version") || await Count(connection, cancellationToken).ConfigureAwait(false) > 0)
            {
                return Array.Empty<string>();
            }

            return _present.Where(t => names.Contains(t) && !names.Contains("ts_" + t)).ToArray();
        }
    }

    private static async Task<long> Count(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = AppliedFour;
            return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
    }

    private static async ValueTask<Result<Unit>> Rename(SqliteConnectionFactory connections, string[] tables, CancellationToken cancellationToken)
    {
        var opened = await connections.Open(cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        var connection = opened.Value;
        await using (connection.ConfigureAwait(false))
        {
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                if (tables.Contains("eval_items", StringComparer.Ordinal))
                {
                    await Run(connection, transaction, RenameItems, cancellationToken).ConfigureAwait(false);
                }

                if (tables.Contains("labels", StringComparer.Ordinal))
                {
                    await Run(connection, transaction, RenameLabels, cancellationToken).ConfigureAwait(false);
                }

                await Run(connection, transaction, DropIndex, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return Results.Ok();
    }

    private static async Task Run(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.Transaction = transaction;
#pragma warning disable CA2100 // only this file's constant statements reach here
            command.CommandText = sql;
#pragma warning restore CA2100
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
