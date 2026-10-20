using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The in-memory aw-packets honours the packets contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryPacketsContractTests : PacketsContract
{
    protected override PacketRef Good { get; } = new(PacketKind.Phase, "/packets/good");

    protected override PacketRef Bad { get; } = new(PacketKind.Phase, "/packets/bad");

    protected override PacketRef Missing { get; } = new(PacketKind.Phase, "/packets/missing");

    protected override IPackets Create() => new InMemoryPackets().Fails(Bad.Directory).Unreadable(Missing.Directory);
}
