using Aw.Kernel.Data;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a <see cref="WorkflowSql.SelectRevision"/> row. A stored spec was valid when it was saved, so a row that doesn't parse is a bug, not input.</summary>
internal sealed class WorkflowRevisionMapper : IRowMapper<WorkflowRevision>
{
    public static readonly WorkflowRevisionMapper Instance = new();

    public WorkflowRevision Map(SqliteDataReader reader) => new(
        WorkflowId.From(reader.GetString(0)),
        reader.GetInt32(1),
        GraphJson.Parse(reader.GetString(2)).Value,
        reader.GetString(3),
        reader.GetString(4),
        SqliteRead.GetTimestamp(reader, 5));
}
