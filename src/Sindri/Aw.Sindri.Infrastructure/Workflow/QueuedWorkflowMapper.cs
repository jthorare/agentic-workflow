using Aw.Kernel.Data;
using Aw.Sindri.Core.Graph.Ids;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a <see cref="WorkflowRunSql.QueuedWorkflows"/> row.</summary>
internal sealed class QueuedWorkflowMapper : IRowMapper<WorkflowId>
{
    public static readonly QueuedWorkflowMapper Instance = new();

    public WorkflowId Map(SqliteDataReader reader) => WorkflowId.From(reader.GetString(0));
}
