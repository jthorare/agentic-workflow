using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Ledger;

[Lane(TestLane.Unit)]
public sealed class LedgerSignalContractTests : LedgerSignalContract
{
    protected override ILedgerSignal Create(TimeProvider clock) => new LedgerSignal(clock);
}
