using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

/// <summary>One matching chunk row as the lexical statement returns it; 'Bm25' is FTS5's own score, lower (more negative) is better.</summary>
/// <param name="Chunk">The chunk's stored key.</param>
/// <param name="Title">The record's title.</param>
/// <param name="LineStart">The first line, or null.</param>
/// <param name="LineEnd">The last line, or null.</param>
/// <param name="Bm25">The bm25 value.</param>
internal sealed record LexicalRow(string Chunk, string Title, int? LineStart, int? LineEnd, double Bm25)
{
    internal static IRowMapper<LexicalRow> Mapper { get; } = new RowMapper();

    internal static IRowMapper<bool> Exists { get; } = new ExistsMapper();

    private sealed class RowMapper : IRowMapper<LexicalRow>
    {
        public LexicalRow Map(SqliteDataReader reader) => new(
            reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.GetDouble(4));
    }

    private sealed class ExistsMapper : IRowMapper<bool>
    {
        public bool Map(SqliteDataReader reader) => reader.GetInt64(0) == 1;
    }
}
