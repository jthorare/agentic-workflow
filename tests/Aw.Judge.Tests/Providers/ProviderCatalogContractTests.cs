using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ProviderCatalogContractTests : ProviderCatalogContract
{
    protected override IProviderCatalog Create(IReadOnlyList<AgentCliName> installed) =>
        ProviderCatalogTests.Catalog(installed, hostProvider: null);
}
