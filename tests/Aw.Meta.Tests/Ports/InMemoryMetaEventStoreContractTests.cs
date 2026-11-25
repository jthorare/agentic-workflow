using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryMetaEventStoreContractTests : MetaEventStoreContract
{
    protected override ValueTask<IMetaEventStore> Create() => ValueTask.FromResult<IMetaEventStore>(new InMemoryMetaEventStore());
}
