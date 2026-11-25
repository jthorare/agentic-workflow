using Aw.Kernel.Data;
using Aw.Sindri.Core.Toolkits;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>Maps a 'toolkit_call' row (<see cref="ToolkitCallSql.Columns"/> order).</summary>
internal sealed class ToolkitCallMapper : IRowMapper<ToolkitCall>
{
    public static readonly ToolkitCallMapper Instance = new();

    public ToolkitCall Map(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetInt32(5) != 0,
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.GetInt64(7),
        SqliteRead.GetTimestamp(reader, 8));
}
