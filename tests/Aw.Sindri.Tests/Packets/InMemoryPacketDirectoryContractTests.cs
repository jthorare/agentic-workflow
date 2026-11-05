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

    protected override string Seed(IPacketDirectory files, IReadOnlyDictionary<string, string> tree) =>
        ((InMemoryPacketDirectory)files).Put("/seed/packet", tree) is var _ ? "/seed/packet" : string.Empty;
}
