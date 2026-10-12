using Aw.Kernel.Data;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>Maps the lease row (LedgerSql.SelectLease column order).</summary>
internal sealed class LeaseRowMapper : IRowMapper<LeaseRow>
{
    public static readonly LeaseRowMapper Instance = new();

    public LeaseRow Map(SqliteDataReader reader) => new(
        new LeaseHolder(HostId.From(reader.GetString(0)), reader.GetInt32(1), SqliteRead.GetTimestamp(reader, 2), BootId.From(reader.GetString(3))),
        reader.GetInt64(4),
        SqliteRead.GetTimestamp(reader, 5),
        SqliteRead.GetTimestamp(reader, 6),
        reader.GetInt64(7) == 1);
}
