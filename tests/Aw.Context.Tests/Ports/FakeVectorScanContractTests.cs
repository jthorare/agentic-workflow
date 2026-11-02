using Aw.Context.Testing.Vectors;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeVectorScanContractTests : VectorScanContract<FakeVectorScan>
{
    protected override FakeVectorScan Create() => new();
}
