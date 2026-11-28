using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a <see cref="WorkflowRunSql.CountAll"/> row.</summary>
internal sealed class RunCountMapper : IRowMapper<int>
{
    public static readonly RunCountMapper Instance = new();

    public int Map(SqliteDataReader reader) => reader.GetInt32(0);
}
