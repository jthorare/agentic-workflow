using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The macOS socket table passes the unix sockets contract over the recorded lsof output.</summary>
[Lane(TestLane.Unit)]
public sealed class MacUnixSocketsContractTests : UnixSocketsContract
{
    protected override IUnixSockets Create() => new MacUnixSockets(new RecordedMacTools(), ownUid: 501);

    protected override IUnixSockets CreateWithoutSource() => new MacUnixSockets(new RecordedMacTools { LsofPresent = false }, ownUid: 501);
}
