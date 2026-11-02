using Aw.Embeddings.Testing;

namespace Aw.Embeddings.Tests.Scan;

[Trait("Lane", "unit")]
public sealed class ScanInProcessContractTests : VectorScanContract
{
    protected override IMatrixScan Arrange() => new ScanInProcess();
}
