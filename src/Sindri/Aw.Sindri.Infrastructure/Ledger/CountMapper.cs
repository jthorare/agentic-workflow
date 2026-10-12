using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>Maps a single integer column (a count or a sequence number).</summary>
internal sealed class CountMapper : IRowMapper<int>, IRowMapper<long>
{
    public static readonly CountMapper Instance = new();

    int IRowMapper<int>.Map(SqliteDataReader reader) => reader.GetInt32(0);

    long IRowMapper<long>.Map(SqliteDataReader reader) => reader.GetInt64(0);
}
