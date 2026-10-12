using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Ids;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>Maps BriefSql's columns to a Brief.</summary>
internal sealed class BriefMapper : IRowMapper<Brief>
{
    internal static readonly BriefMapper Instance = new();

    public Brief Map(SqliteDataReader reader) => new()
    {
        ToolUseId = ToolUseId.From(reader.GetString(0)),
        AgentId = reader.IsDBNull(1) ? null : AgentId.From(reader.GetString(1)),
        SessionId = SessionId.From(reader.GetString(2)),
        PromptId = PromptId.From(reader.GetString(3)),
        DispatchName = reader.IsDBNull(4) ? null : reader.GetString(4),
        SubagentType = reader.GetString(5),
        Goal = reader.GetString(6),
        AcceptanceCriteria = reader.GetString(7),
        ProofCommand = reader.GetString(8),
        SavedAt = SqliteRead.GetTimestamp(reader, 9),
    };
}
