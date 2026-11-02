using Aw.Context.Testing.Vectors;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeQueryEmbedderContractTests : QueryEmbedderContract<FakeQueryEmbedder>
{
    protected override FakeQueryEmbedder Create() => new();
}
