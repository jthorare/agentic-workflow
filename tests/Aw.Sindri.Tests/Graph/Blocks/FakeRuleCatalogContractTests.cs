using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>Binds <see cref="FakeRuleCatalog"/> to <see cref="RuleCatalogPortContract"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeRuleCatalogContractTests : RuleCatalogPortContract
{
    protected override ValueTask<IRuleCatalogPort> Create(IReadOnlyList<RuleCatalogSeed> rules) => ValueTask.FromResult<IRuleCatalogPort>(new FakeRuleCatalog(rules));
}
