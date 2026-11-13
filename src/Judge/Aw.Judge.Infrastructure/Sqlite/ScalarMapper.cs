using Aw.Judge.Core.Decisions;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>Maps a one-column row: a COUNT(*) or a stored outcome.</summary>
internal static class ScalarMapper
{
    internal static readonly IRowMapper<int> Count = new CountMapper();
    internal static readonly IRowMapper<DecisionOutcome> Outcome = new OutcomeMapper();
    internal static readonly IRowMapper<FailureRecord> Failure = new FailureMapper();

    private sealed class CountMapper : IRowMapper<int>
    {
        public int Map(SqliteDataReader reader) => checked((int)reader.GetInt64(0));
    }

    private sealed class FailureMapper : IRowMapper<FailureRecord>
    {
        public FailureRecord Map(SqliteDataReader reader) =>
            new(SqliteRead.GetTimestamp(reader, 0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }

    private sealed class OutcomeMapper : IRowMapper<DecisionOutcome>
    {
        public DecisionOutcome Map(SqliteDataReader reader) => DecisionText.Outcome(reader.GetString(0));
    }
}
