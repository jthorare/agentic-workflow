using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The one-column reads the side-row writers compare stored rows with.</summary>
internal static class SideRowReads
{
    internal static IRowMapper<long> Number { get; } = new NumberMapper();

    internal static IRowMapper<string> Text { get; } = new TextMapper();

    private sealed class NumberMapper : IRowMapper<long>
    {
        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class TextMapper : IRowMapper<string>
    {
        public string Map(SqliteDataReader reader) => reader.GetString(0);
    }
}
