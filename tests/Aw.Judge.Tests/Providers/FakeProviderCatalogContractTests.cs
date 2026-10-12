using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class FakeProviderCatalogContractTests : ProviderCatalogContract
{
    protected override IProviderCatalog Create(IReadOnlyList<AgentCliName> installed) =>
        new FakeProviderCatalog([
            new ScriptedProvider(ProviderName.Jev, [.. ContentClasses.Text]),
            .. installed.Select(cli => new ScriptedProvider(AgentCliNames.Provider(cli), [.. ContentClasses.All])),
        ]);
}
