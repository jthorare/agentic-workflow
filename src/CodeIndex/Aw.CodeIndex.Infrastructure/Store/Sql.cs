using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>Small ADO.NET helpers: every command text is a constant at the call site and every value is a parameter.</summary>
internal static class Sql
{
    /// <summary>Makes a command; the caller disposes it.</summary>
    internal static SqliteCommand Command(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
#pragma warning disable CA2100 // every caller passes a constant command text; its values go in parameters
        command.CommandText = sql;
#pragma warning restore CA2100
        command.Transaction = transaction;
        return command;
    }

    internal static async Task<int> Run(SqliteConnection connection, string sql, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        var command = Command(connection, sql, transaction);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<object?> Scalar(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        var command = Command(connection, sql);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<long> Count(SqliteConnection connection, string sql, CancellationToken cancellationToken)
        => Convert.ToInt64(await Scalar(connection, sql, cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);

    internal static byte[]? Blob(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Bytes(reader, ordinal);

    /// <summary>A blob column's bytes; any other stored value is damage the store guard reports (#2242).</summary>
    internal static byte[] Bytes(SqliteDataReader reader, int ordinal)
        => reader.GetValue(ordinal) as byte[]
            ?? throw new InvalidDataException($"the stored {reader.GetName(ordinal)} is not a blob");

    internal static string? Text(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    internal static SqliteCommand With(this SqliteCommand command, string name, object? value)
    {
        _ = command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
}
