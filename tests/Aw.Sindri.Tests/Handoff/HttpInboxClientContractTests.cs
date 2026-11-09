using Aw.Kernel.Testing;
using Aw.Sindri.Testing.Handoff;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>The inbox contract over HttpInboxClient, against the contract's HTTP side on loopback.</summary>
[Lane(TestLane.Integration)]
public sealed class HttpInboxClientContractTests : InboxContract
{
    protected override InboxUnderTest Create() => InboxPairs.Http();
}
