using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class InMemoryConnectionStoreContractTests : ConnectionStoreContract
{
    protected override IConnectionStore Create() => new InMemoryConnectionStore();
}
