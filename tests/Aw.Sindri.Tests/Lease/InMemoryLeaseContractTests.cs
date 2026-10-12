using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Lease;

[Lane(TestLane.Unit)]
public sealed class InMemoryLeaseContractTests : LeaseContract
{
    protected override ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe)
    {
        var store = new InMemorySindriStore(clock, probe);
        return ValueTask.FromResult(new SindriStores(store, store));
    }
}
