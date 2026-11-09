using Aw.Kernel.Testing;
using Aw.Sindri.Testing.Handoff;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>The inbox contract over the in-memory fake.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeInboxContractTests : InboxContract
{
    protected override InboxUnderTest Create() => InboxPairs.Fake();
}
