using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The in-memory packet files honour the packet-files contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryPacketFilesContractTests : PacketFilesContract
{
    /// <inheritdoc/>
    protected override string Directory => "/run/T-1/scoping/1/scope-packet";

    /// <inheritdoc/>
    protected override IPacketFiles Create() => new InMemoryPacketFiles().Put(Directory, "scope-map.json", "{\"a\":1}");
}
