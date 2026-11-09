using Aw.Kernel.Data;
using Aw.Sindri.Core.Memory;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Memory;

/// <summary>Maps a 'memory' row (<see cref="MemorySql.MemoryColumns"/> order).</summary>
internal sealed class MemoryInfoMapper : IRowMapper<MemoryInfo>
{
    public static readonly MemoryInfoMapper Instance = new();

    public MemoryInfo Map(SqliteDataReader reader) => new(
        new MemoryName { Value = reader.GetString(0) },
        reader.IsDBNull(1) ? null : reader.GetString(1),
        reader.GetString(2),
        SqliteRead.GetTimestamp(reader, 3));
}
