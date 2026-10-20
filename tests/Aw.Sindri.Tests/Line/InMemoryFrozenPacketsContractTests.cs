using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Line;

/// <summary>The in-memory frozen packets honour the freeze-record contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryFrozenPacketsContractTests : FrozenPacketsContract
{
    protected override IFrozenPackets Create(IReadOnlyList<FrozenPacket> held) => new InMemoryFrozenPackets(held);
}
