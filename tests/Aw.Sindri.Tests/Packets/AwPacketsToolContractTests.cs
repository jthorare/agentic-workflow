using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The real aw-packets, through the adapter, honours the packets contract the in-memory fake does (a machine freeze first, an owner freeze after: alreadyFrozenBy says so).</summary>
[Lane(TestLane.Integration)]
public sealed class AwPacketsToolContractTests : PacketsContract, IDisposable
{
    private readonly RealAwPackets _real = new();

    public AwPacketsToolContractTests()
    {
        Good = _real.Packet("good");
        Bad = _real.Packet("bad");
        File.Delete(Path.Combine(Bad.Directory, "signals.json"));
        Missing = new PacketRef(PacketKind.Design, Path.Combine(_real.HomePath, "missing"), Repo: Good.Repo);
    }

    protected override PacketRef Good { get; }

    protected override PacketRef Bad { get; }

    protected override PacketRef Missing { get; }

    public void Dispose() => _real.Dispose();

    protected override IPackets Create() => _real.Tool();
}
