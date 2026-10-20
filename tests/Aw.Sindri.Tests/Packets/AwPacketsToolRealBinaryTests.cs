using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Tests.Packets;

/// <summary>
/// The aw-packets adapter against the real built aw-packets: the JSON it reads, on the line fixture's gold design packet. Freeze writes under a temp AW_STATE_DIR set by the wrapper script.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class AwPacketsToolRealBinaryTests : IDisposable
{
    private readonly RealAwPackets _real = new();

    public void Dispose() => _real.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Validate_hash_and_freeze_read_what_the_real_aw_packets_prints()
    {
        var tool = _real.Tool();
        var packet = _real.Packet("good");

        var validation = await tool.Validate(packet, Token);
        var hash = await tool.Hash(packet, Token);
        var again = await tool.Hash(packet, Token);
        var first = await tool.Freeze(packet, PacketFreezer.Owner, Token);
        var second = await tool.Freeze(packet, PacketFreezer.Machine, Token);

        Assert.True(validation.Value.Passed, string.Join("; ", validation.Value.Failures));
        Assert.Matches("^sha256:[0-9a-f]{64}$", hash.Value);
        Assert.Equal(hash.Value, again.Value);
        Assert.True(first.Value.Frozen);
        Assert.False(first.Value.AlreadyFrozen);
        Assert.Equal(hash.Value, first.Value.Hash);
        Assert.Equal(PacketFreezer.Owner, first.Value.By);   // the adapter's fallback is Machine, so only a 'by' it read can be Owner
        Assert.True(second.Value.Frozen);
        Assert.True(second.Value.AlreadyFrozen);
        Assert.Equal(PacketFreezer.Owner, second.Value.By);
        Assert.Equal(PacketFreezer.Owner, second.Value.AlreadyFrozenBy);
        Assert.Equal(hash.Value, second.Value.Hash);
    }

    [Fact]
    public async Task A_broken_packet_fails_validate_with_its_rule_and_freezes_nothing()
    {
        var tool = _real.Tool();
        var packet = _real.Packet("broken");
        File.Delete(Path.Combine(packet.Directory, "signals.json"));

        var validation = await tool.Validate(packet, Token);
        var freeze = await tool.Freeze(packet, PacketFreezer.Machine, Token);

        Assert.False(validation.Value.Passed);
        Assert.NotEmpty(validation.Value.Failures);
        Assert.All(validation.Value.Failures, failure => Assert.StartsWith("PKT-", failure.Rule, StringComparison.Ordinal));
        Assert.False(freeze.Value.Frozen);
        Assert.NotEmpty(freeze.Value.Failures);
        Assert.False(Directory.Exists(Path.Combine(_real.HomePath, "state", "packets")));
    }
}
