using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeSourceIdentityStoreContractTests : SourceIdentityStoreContract<FakeSourceIdentityStore>
{
    protected override FakeSourceIdentityStore Create() => new();
}
