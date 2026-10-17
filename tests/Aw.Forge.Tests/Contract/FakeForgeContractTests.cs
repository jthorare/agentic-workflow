using Aw.Forge.Testing;

namespace Aw.Forge.Tests.Contract;

[Trait("Lane", "unit")]
public sealed class FakeForgeContractTests : ForgeContract
{
    protected override ForgeUnderTest Create() => ForgeWorlds.Fake();
}
