using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a <see cref="WorkflowRunSql.SelectTick"/> row.</summary>
internal sealed class TickMapper : IRowMapper<DateTimeOffset>
{
    public static readonly TickMapper Instance = new();

    public DateTimeOffset Map(SqliteDataReader reader) => SqliteRead.GetTimestamp(reader, 0);
}
