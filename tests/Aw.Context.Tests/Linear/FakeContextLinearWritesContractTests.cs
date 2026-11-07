using Aw.Context.Core.Linear;
using Aw.Context.Testing.Linear;

namespace Aw.Context.Tests.Linear;

[Trait("Lane", "unit")]
public sealed class FakeContextLinearWritesContractTests : ContextLinearWritesContract<FakeContextLinearWrites>
{
    protected override FakeContextLinearWrites Create() => new();

    protected override int Sent(FakeContextLinearWrites writes) => writes.Calls.Count;

    protected override IReadOnlyList<LinearClientId> ClientIdsSent(FakeContextLinearWrites writes) => [.. writes.Calls.Where(c => c.ClientId is not null).Select(c => c.ClientId!)];

    protected override void MakeNextWriteUnknown(FakeContextLinearWrites writes) => writes.Next(new LinearWriteUnknown());
}
