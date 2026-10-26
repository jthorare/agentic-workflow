using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>Maps DecisionSql.Find's columns: the decision (0-14), then its detail row (15-21, null when absent).</summary>
/// <param name="json">Reads the JSON text columns.</param>
internal sealed class DecisionMapper(DecisionJson json) : IRowMapper<DecisionRecord>
{
    public DecisionRecord Map(SqliteDataReader reader) => new()
    {
        Id = DecisionId.From(reader.GetString(0)),
        Ts = SqliteRead.GetTimestamp(reader, 1),
        Question = reader.GetString(2),
        ContentClass = reader.GetString(3),
        Provider = reader.GetString(4),
        Decision = reader.IsDBNull(5) ? null : reader.GetString(5),
        Confidence = reader.GetDouble(6),
        ReasonCode = reader.GetString(7),
        LatencyMs = reader.GetInt64(8),
        InputDigest = reader.GetString(9),
        UndoneAt = reader.IsDBNull(10) ? null : SqliteRead.GetTimestamp(reader, 10),
        ChainPosition = reader.GetInt32(11),
        Skipped = json.Skipped(reader.GetString(12)),
        Outcome = DecisionText.Outcome(reader.GetString(13)),
        ApprovedAt = reader.IsDBNull(14) ? null : SqliteRead.GetTimestamp(reader, 14),
        Details = reader.IsDBNull(15) ? null : Details(reader),
    };

    private DecisionDetails Details(SqliteDataReader reader) => new()
    {
        InputJson = reader.GetString(16),
        Probabilities = json.Probabilities(reader.IsDBNull(17) ? null : reader.GetString(17)),
        RulesOpinion = reader.IsDBNull(18) ? null : reader.GetString(18),
        Agreement = reader.IsDBNull(19) ? null : DecisionText.AgreementOf(reader.GetString(19)),
        SessionId = reader.IsDBNull(20) ? null : SessionId.From(reader.GetString(20)),
        Reasons = json.Reasons(reader.IsDBNull(21) ? null : reader.GetString(21)),
    };
}
