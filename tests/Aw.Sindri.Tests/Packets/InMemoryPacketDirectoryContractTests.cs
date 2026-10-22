using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The in-memory packet files honour the packet-files contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryPacketDirectoryContractTests : PacketDirectoryContract
{
    protected override string Nothing => "/no/such/packet";

    protected override IPacketDirectory Create() => new InMemoryPacketDirectory();
}
