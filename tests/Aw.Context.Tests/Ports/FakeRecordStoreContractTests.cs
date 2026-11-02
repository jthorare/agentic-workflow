using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeRecordStoreContractTests : RecordStoreContract<FakeRecordStore>
{
    protected override FakeRecordStore Create() => new();
}
