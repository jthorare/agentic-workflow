using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>The SQLite lease and ledger over one ledger file in a home directory.</summary>
internal static class SqliteStores
{
    internal static SindriStores Create(string home, TimeProvider clock, IProcessProbe probe)
    {
        var database = new LedgerDatabase(new SindriSettings(home, Path.Combine(home, "profile.yaml")), clock);
        return new SindriStores(new SqliteLease(database, probe, clock), new SqliteLedger(database));
    }
}
