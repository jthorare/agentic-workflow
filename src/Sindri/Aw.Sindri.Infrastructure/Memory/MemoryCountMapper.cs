using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Memory;

/// <summary>Maps a (memory name, fact count) row.</summary>
internal sealed class MemoryCountMapper : IRowMapper<KeyValuePair<string, int>>
{
    public static readonly MemoryCountMapper Instance = new();

    public KeyValuePair<string, int> Map(SqliteDataReader reader) => new(reader.GetString(0), reader.GetInt32(1));
}
