using Aw.Kernel.Data;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a <see cref="WorkflowRunSql.SelectRun"/> row.</summary>
internal sealed class WorkflowRunMapper : IRowMapper<WorkflowRun>
{
    public static readonly WorkflowRunMapper Instance = new();

    public WorkflowRun Map(SqliteDataReader reader) => new(
        WorkflowRunId.From(reader.GetString(0)),
        WorkflowId.From(reader.GetString(1)),
        reader.GetInt32(2),
        reader.GetString(3),
        RunStatusText.Run(reader.GetString(4)),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : ItemId.From(reader.GetString(7)),
        SqliteRead.GetTimestamp(reader, 8),
        reader.IsDBNull(9) ? null : SqliteRead.GetTimestamp(reader, 9),
        reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11));
}
