using Aw.Kernel.Data;
using Aw.Sindri.Core.Views;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>Maps the projector_state row (ViewSql.SelectState column order); Fresh is set by Open.</summary>
internal sealed class ProjectorStateMapper : IRowMapper<ProjectorState>
{
    public static readonly ProjectorStateMapper Instance = new();

    public ProjectorState Map(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetInt32(1),
        reader.IsDBNull(2) ? null : SqliteRead.GetTimestamp(reader, 2),
        reader.GetInt64(3) == 1,
        reader.IsDBNull(4) ? null : reader.GetInt64(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        Fresh: false,
        RebuiltThrough: reader.IsDBNull(6) ? null : reader.GetInt64(6));
}
