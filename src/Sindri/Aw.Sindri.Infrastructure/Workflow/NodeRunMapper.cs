using Aw.Kernel.Data;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a <see cref="WorkflowRunSql.SelectNodeRuns"/> row.</summary>
internal sealed class NodeRunMapper : IRowMapper<NodeRun>
{
    public static readonly NodeRunMapper Instance = new();

    public NodeRun Map(SqliteDataReader reader) => new(
        WorkflowRunId.From(reader.GetString(0)),
        NodeId.From(reader.GetString(1)),
        reader.GetInt32(2),
        RunStatusText.Node(reader.GetString(3)),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetInt32(6) != 0,
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : SqliteRead.GetTimestamp(reader, 10),
        reader.IsDBNull(11) ? null : SqliteRead.GetTimestamp(reader, 11));
}
