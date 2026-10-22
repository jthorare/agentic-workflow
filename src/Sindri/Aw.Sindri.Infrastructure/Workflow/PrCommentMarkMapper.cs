using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a 'pr_comment_marks' row (<see cref="PrCommentMarkSql.Columns"/> order).</summary>
internal sealed class PrCommentMarkMapper : IRowMapper<PrCommentMark>
{
    public static readonly PrCommentMarkMapper Instance = new();

    public PrCommentMark Map(SqliteDataReader reader) => new(
        new PrRef(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)),
        PrCommentId.From(reader.GetString(3)),
        reader.GetString(4) is { Length: > 0 } edited ? edited : null,
        InstanceAddress.From(reader.GetString(5)),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        SqliteRead.GetTimestamp(reader, 9),
        reader.GetString(10));
}
