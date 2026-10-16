using System.Data.Common;
using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>
/// A query connection on the copy, read-only at the connection (Mode=ReadOnly and query_only). SQLite has no connection
/// setting that stops ATTACH or VACUUM INTO from creating a file, so a second statement and those statements are refused here
/// before SQLite sees them.
/// </summary>
internal sealed class SqliteReadSession(SqliteConnection connection) : IReadSession
{
    private const int ReadOnlyCode = 8;
    private const int AuthorizationCode = 23;

    private static readonly string[] _bypassStarts = ["ATTACH", "DETACH", "VACUUM"];

    /// <inheritdoc/>
    public async ValueTask<Result<Rows>> QueryAsync(string sql, int rowLimit, CancellationToken cancellationToken)
    {
        if (Refusal(sql) is { } refusal)
        {
            return EvidenceErrors.DatabaseRefused.With(refusal);
        }

        try
        {
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = sql;
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    return await ReadAsync(reader, rowLimit, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is ReadOnlyCode or AuthorizationCode)
        {
            return EvidenceErrors.DatabaseRefused.With($"the database refused a write (SQLite error {exception.SqliteErrorCode.ToString(CultureInfo.InvariantCulture)}): {exception.Message}");
        }
        catch (SqliteException exception)
        {
            return EvidenceErrors.RunFailed.With($"the query failed: {exception.Message}");
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => connection.DisposeAsync();

    private static string? Refusal(string sql)
    {
        var statements = SqlText.Statements(sql, nestedComments: false);
        if (statements.Count != 1)
        {
            return "exactly one statement runs at a time";
        }

        var first = SqlText.Words(statements[0]) is [var word, ..] ? word : string.Empty;
        if (_bypassStarts.Contains(first, StringComparer.Ordinal))
        {
            return $"{first} would reach beyond the read-only copy";
        }

        return first == "PRAGMA" && !SqlText.IsReadPragma(statements[0]) ? "a PRAGMA that assigns or is not a schema read is refused" : null;
    }

    private static async ValueTask<Result<Rows>> ReadAsync(SqliteDataReader reader, int rowLimit, CancellationToken cancellationToken)
    {
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var rows = new List<IReadOnlyList<object?>>();
        var truncated = false;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (rows.Count == rowLimit)
            {
                truncated = true;
                break;
            }

            rows.Add([.. Enumerable.Range(0, columns.Count).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i))]);
        }

        return new Rows(columns, rows, truncated, SourceColumns(reader));
    }

    // The table column each result column reads, when SQLite can say (a direct column reference; a computed column has none).
    private static List<string?> SourceColumns(SqliteDataReader reader)
    {
        try
        {
            return [.. reader.GetColumnSchema().Select(static column => column.BaseColumnName)];
        }
        catch (NotSupportedException)
        {
            return [.. Enumerable.Repeat<string?>(element: null, count: reader.FieldCount)];
        }
    }
}
