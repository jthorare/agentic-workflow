using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The disk packet files honour the packet-files contract.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskPacketDirectoryContractTests : PacketDirectoryContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override string Nothing => Path.Combine(_home.Path, "no-such-packet");

    protected override IPacketDirectory Create() => new DiskPacketDirectory(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")));
}
