using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class InMemoryPollCursorsContractTests : PollCursorsContract
{
    protected override IPollCursors Create() => new InMemoryPollCursors();
}
