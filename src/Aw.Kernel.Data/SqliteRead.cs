using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Reads the values <see cref="SqliteBind"/> writes, strictly, for use in row mappers.</summary>
public static class SqliteRead
{
    // 'F' digits are optional, so one format takes every count of fractional digits, none included.
    private static readonly string[] _formats = ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'"];

    /// <summary>
    /// Reads a Guid stored as lowercase hyphenated TEXT (what <see cref="SqliteBind"/> writes). Anything else (a BLOB, upper case, braces, junk) is a data bug and throws, so a row never round-trips
    /// to different bytes.
    /// </summary>
    /// <param name="reader">A reader on a row.</param>
    /// <param name="ordinal">The column.</param>
    /// <returns>The Guid.</returns>
    /// <exception cref="InvalidDataException">The column isn't a lowercase hyphenated Guid.</exception>
    public static Guid GetGuid(SqliteDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var value = reader.GetValue(ordinal);
        if (value is string text
            && Guid.TryParseExact(text, "D", out var guid)
            && string.Equals(text, guid.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return guid;
        }

        throw new InvalidDataException(
            $"Column {reader.GetName(ordinal)} holds {value.GetType().Name} '{value}', not a lowercase hyphenated Guid.");
    }

    /// <summary>
    /// Reads a timestamp stored as ISO-8601 UTC TEXT as a <see cref="DateTimeOffset"/> at offset zero: the round-trip form
    /// <see cref="SqliteBind"/> writes ('2026-10-10T07:05:07.0420000Z') and any other count of fractional digits, none
    /// included (the TS judge writes milliseconds). Text without the 'Z', with another offset, or not a time is a data bug and throws.
    /// </summary>
    /// <param name="reader">A reader on a row.</param>
    /// <param name="ordinal">The column.</param>
    /// <returns>The timestamp.</returns>
    /// <exception cref="InvalidDataException">The column isn't ISO-8601 UTC text.</exception>
    public static DateTimeOffset GetTimestamp(SqliteDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var value = reader.GetValue(ordinal);
        if (value is string text
            && text.EndsWith('Z')
            && DateTimeOffset.TryParseExact(text, _formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            return timestamp;
        }

        throw new InvalidDataException(
            $"Column {reader.GetName(ordinal)} holds {value.GetType().Name} '{value}', not a UTC ISO-8601 timestamp.");
    }

    /// <summary>
    /// Reads TEXT at <paramref name="ordinal"/> and parses it. Any failure (the column isn't text, or <paramref name="parse"/> refuses the value, a Vogen id included) is a <see cref="StoredRowException"/> naming the table,
    /// the column and the row key, never the value, so a bad stored value is a store fault and not a request one (#2229).
    /// </summary>
    /// <param name="reader">A reader on a row.</param>
    /// <param name="ordinal">The column.</param>
    /// <param name="parse">Parses the text.</param>
    /// <param name="table">The table the column belongs to, for the message.</param>
    /// <param name="rowKey">The row's key when the mapper already read it, for the message.</param>
    /// <typeparam name="T">The parsed type.</typeparam>
    /// <returns>The parsed value.</returns>
    /// <exception cref="StoredRowException">The column can't be read as <typeparamref name="T"/>.</exception>
    public static T Parse<T>(SqliteDataReader reader, int ordinal, Func<string, T> parse, string? table = null, string? rowKey = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(parse);
        try
        {
            return parse(reader.GetString(ordinal));
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or StoredRowException))   // boundary: whatever a parser throws on a stored value is the store's fault
        {
            throw new StoredRowException(table, reader.GetName(ordinal), rowKey, statement: null, exception);
        }
    }

    /// <summary>Like <see cref="Parse{T}"/> for a nullable TEXT column: NULL reads as null.</summary>
    /// <param name="reader">A reader on a row.</param>
    /// <param name="ordinal">The column.</param>
    /// <param name="parse">Parses the text.</param>
    /// <param name="table">The table the column belongs to, for the message.</param>
    /// <param name="rowKey">The row's key when the mapper already read it, for the message.</param>
    /// <typeparam name="T">The parsed type.</typeparam>
    /// <returns>The parsed value, or null for NULL.</returns>
    /// <exception cref="StoredRowException">The column can't be read as <typeparamref name="T"/>.</exception>
    public static T? ParseOrNull<T>(SqliteDataReader reader, int ordinal, Func<string, T> parse, string? table = null, string? rowKey = null)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.IsDBNull(ordinal) ? null : Parse(reader, ordinal, parse, table, rowKey);
    }
}
