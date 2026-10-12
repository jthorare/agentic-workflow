using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Ledger;

[Lane(TestLane.Integration)]
public sealed class SqliteLedgerStoreContractTests : LedgerStoreContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe) =>
        ValueTask.FromResult(SqliteStores.Create(_home.Path, clock, probe));
}
