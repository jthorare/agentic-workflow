using Aw.Kernel.Data;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a <see cref="WorkflowSql.SelectRecord"/> row.</summary>
internal sealed class WorkflowRecordMapper : IRowMapper<WorkflowRecord>
{
    public static readonly WorkflowRecordMapper Instance = new();

    public WorkflowRecord Map(SqliteDataReader reader) => new(
        WorkflowId.From(reader.GetString(0)),
        WorkflowSlug.From(reader.GetString(1)),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetInt32(3),
        reader.GetInt32(4),
        reader.IsDBNull(5) ? null : SqliteRead.GetTimestamp(reader, 5),
        reader.GetString(6),
        reader.GetInt32(7) != 0);
}
