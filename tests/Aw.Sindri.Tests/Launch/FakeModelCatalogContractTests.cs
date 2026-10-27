using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

[Lane(TestLane.Unit)]
public sealed class FakeModelCatalogContractTests : ModelCatalogContract
{
    protected override IModelCatalog Create(AgentId agent, IReadOnlyList<ModelFacts> models) => new FakeModelCatalog().With(agent, [.. models]);
}
