using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeRecordFeedContractTests : RecordFeedContract<FakeRecordFeed>
{
    protected override FakeRecordFeed Create(IReadOnlyList<RecordItem> items) => new(RecordSamples.Linear, items);
}
