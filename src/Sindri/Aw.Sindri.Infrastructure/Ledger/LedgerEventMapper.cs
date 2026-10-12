using Aw.Kernel.Data;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Ledger;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>Maps an events row (LedgerSql's event column order).</summary>
internal sealed class LedgerEventMapper : IRowMapper<LedgerEvent>
{
    public static readonly LedgerEventMapper Instance = new();

    public LedgerEvent Map(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetInt64(1),
        new EventEnvelope<string>(
            SqliteRead.GetGuid(reader, 2),
            reader.GetString(3),
            reader.GetInt32(4),
            SqliteRead.GetTimestamp(reader, 5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetInt32(8),
            SqliteRead.GetGuid(reader, 9),
            reader.IsDBNull(10) ? null : SqliteRead.GetGuid(reader, 10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.GetString(12)),
        reader.GetString(13),
        reader.IsDBNull(14) ? null : reader.GetString(14));
}
