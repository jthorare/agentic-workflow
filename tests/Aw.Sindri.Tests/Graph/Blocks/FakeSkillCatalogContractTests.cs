using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>Binds <see cref="FakeSkillCatalog"/> to <see cref="SkillCatalogPortContract"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeSkillCatalogContractTests : SkillCatalogPortContract
{
    protected override ValueTask<ISkillCatalogPort> Create(IReadOnlyList<SkillCatalogSeed> skills) => ValueTask.FromResult<ISkillCatalogPort>(new FakeSkillCatalog(skills));
}
