using Aw.Context.Testing.Vectors;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeChunkEmbedderContractTests : ChunkEmbedderContract<FakeChunkEmbedder>
{
    protected override FakeChunkEmbedder Create() => new();
}
