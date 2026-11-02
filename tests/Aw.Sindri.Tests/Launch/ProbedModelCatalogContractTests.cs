using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

[Lane(TestLane.Unit)]
public sealed class ProbedModelCatalogContractTests : ModelCatalogContract
{
    protected override IModelCatalog Create(AgentId agent, IReadOnlyList<ModelFacts> models) =>
        new ProbedModelCatalog((asked, _) => ValueTask.FromResult(asked.Equals(agent) ? models : null));
}
