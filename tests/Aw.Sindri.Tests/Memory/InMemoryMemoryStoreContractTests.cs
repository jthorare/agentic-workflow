using Aw.Kernel.Testing;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

[Lane(TestLane.Unit)]
public sealed class InMemoryMemoryStoreContractTests : MemoryStoreContract
{
    protected override IMemoryStore Create() => new InMemoryMemoryStore();
}
