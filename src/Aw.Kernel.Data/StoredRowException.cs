namespace Aw.Kernel.Data;

/// <summary>
/// A stored row a mapper can't read (#2229): the store holds a value its type refuses. It is a store fault, not a request fault, so a host answers it 5xx. The message names the table, the column and the row's key when
/// known, and the statement; it never holds the stored value. <see cref="SqliteQueries"/> wraps what a row mapper throws in it, except an <see cref="InvalidDataException"/>, which already says the row can't be
/// read and keeps the message its callers show (judge's unreadable decision, the code index's store fault).
/// </summary>
public sealed class StoredRowException : Exception
{
    /// <summary>A stored row can't be read.</summary>
    public StoredRowException()
        : base("A stored row can't be read")
    {
    }

    /// <summary>A stored row can't be read, in the caller's words.</summary>
    /// <param name="message">The message.</param>
    public StoredRowException(string message)
        : base(message)
    {
    }

    /// <summary>A stored row can't be read, in the caller's words.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">What failed.</param>
    public StoredRowException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Names the part of the store that can't be read.</summary>
    /// <param name="table">The table, or null when unknown.</param>
    /// <param name="column">The column, or null when unknown.</param>
    /// <param name="rowKey">The row's key, or null when unknown. A key, never the unreadable value.</param>
    /// <param name="statement">What the statement was reading, or null.</param>
    /// <param name="innerException">What the mapper threw.</param>
    public StoredRowException(string? table, string? column, string? rowKey, string? statement, Exception innerException)
        : base(Describe(table, column, rowKey, statement), innerException)
    {
        Table = table;
        Column = column;
        RowKey = rowKey;
    }

    /// <summary>The table, or null when unknown.</summary>
    public string? Table { get; }

    /// <summary>The column, or null when unknown.</summary>
    public string? Column { get; }

    /// <summary>The row's key, or null when unknown.</summary>
    public string? RowKey { get; }

    private static string Describe(string? table, string? column, string? rowKey, string? statement)
    {
        var where = (table, column) switch
        {
            (null, null) => "A stored row",
            (null, _) => $"Column {column}",
            (_, null) => $"Table {table}",
            _ => $"{table}.{column}",
        };
        var of = rowKey is null ? string.Empty : $" of row {rowKey}";
        var reading = statement is null ? string.Empty : $" (reading {statement})";
        return $"{where}{of} can't be read{reading}";
    }
}
