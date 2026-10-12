using Aw.Judge.Core.Decisions;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>Maps a one-column row: a COUNT(*) or a stored outcome.</summary>
internal static class ScalarMapper
{
    internal static readonly IRowMapper<int> Count = new CountMapper();
    internal static readonly IRowMapper<DecisionOutcome> Outcome = new OutcomeMapper();

    private sealed class CountMapper : IRowMapper<int>
    {
        public int Map(SqliteDataReader reader) => checked((int)reader.GetInt64(0));
    }

    private sealed class OutcomeMapper : IRowMapper<DecisionOutcome>
    {
        public DecisionOutcome Map(SqliteDataReader reader) => DecisionText.Outcome(reader.GetString(0));
    }
}
