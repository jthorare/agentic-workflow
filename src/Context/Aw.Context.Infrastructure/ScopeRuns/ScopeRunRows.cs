using Aw.Context.Core.ScopeGraph;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.ScopeRuns;

/// <summary>The row mappers of the scope-run store.</summary>
internal static class ScopeRunRows
{
    public static readonly IRowMapper<long> Count = new CountMapper();
    public static readonly IRowMapper<GraphRead> Read = new ReadMapper();

    private sealed class CountMapper : IRowMapper<long>
    {
        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class ReadMapper : IRowMapper<GraphRead>
    {
        public GraphRead Map(SqliteDataReader r) =>
            new(r.GetString(0), r.GetString(1), r.GetInt64(2) == 1, r.IsDBNull(3) ? null : r.GetString(3), SqliteRead.GetTimestamp(r, 4));
    }
}
