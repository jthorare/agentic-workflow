using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeLinearLookupStoreContractTests : LinearLookupStoreContract<FakeLinearLookupStore>
{
    protected override FakeLinearLookupStore Create() => new();
}
