using Aw.Sindri.Core.Packets;
using Xunit;

namespace Aw.Sindri.Testing.Packets;

/// <summary>The contract every <see cref="IPackets"/> honours: a failed check is an answer, a freeze is write-once, the hash is stable.</summary>
public abstract class PacketsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The packet <see cref="Create"/> holds as good.</summary>
    protected abstract PacketRef Good { get; }

    /// <summary>The packet <see cref="Create"/> holds as failing its checks.</summary>
    protected abstract PacketRef Bad { get; }

    /// <summary>The packet <see cref="Create"/> cannot read.</summary>
    protected abstract PacketRef Missing { get; }

    /// <summary>Packets that hold <see cref="Good"/> good, <see cref="Bad"/> failing its checks and <see cref="Missing"/> unreadable.</summary>
    /// <returns>The port.</returns>
    protected abstract IPackets Create();

    /// <summary>A good packet passes with no failures.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_good_packet_validates()
    {
        var result = await Create().Validate(Good, Token);

        Expect.True(result.IsSuccess);
        Expect.True(result.Value.Passed);
        Expect.Equal(0, result.Value.Failures.Count);
    }

    /// <summary>A failed check is an answer with its failures, not an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failing_packet_validates_to_its_failures()
    {
        var result = await Create().Validate(Bad, Token);

        Expect.True(result.IsSuccess);
        Expect.True(!result.Value.Passed);
        Expect.True(result.Value.Failures.Count > 0);
    }

    /// <summary>A packet the tool cannot read is an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unreadable_packet_is_an_error()
    {
        var result = await Create().Validate(Missing, Token);

        Expect.True(result.IsError);
    }

    /// <summary>The hash is sha256 text and the same on every call.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_hash_is_stable()
    {
        var packets = Create();

        var first = await packets.Hash(Good, Token);
        var second = await packets.Hash(Good, Token);

        Expect.True(first.IsSuccess);
        Expect.True(first.Value.StartsWith("sha256:", StringComparison.Ordinal));
        Expect.Equal(first.Value, second.Value);
    }

    /// <summary>A good packet freezes at its hash.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_good_packet_freezes_at_its_hash()
    {
        var packets = Create();

        var frozen = await packets.Freeze(Good, PacketFreezer.Machine, Token);
        var hash = await packets.Hash(Good, Token);

        Expect.True(frozen.IsSuccess);
        Expect.True(frozen.Value.Frozen);
        Expect.True(!frozen.Value.AlreadyFrozen);
        Expect.Equal(hash.Value, frozen.Value.Hash);
        Expect.Equal(PacketFreezer.Machine, frozen.Value.By);
    }

    /// <summary>A packet that fails a check freezes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failing_packet_does_not_freeze()
    {
        var frozen = await Create().Freeze(Bad, PacketFreezer.Owner, Token);

        Expect.True(frozen.IsSuccess);
        Expect.True(!frozen.Value.Frozen);
        Expect.True(frozen.Value.Hash is null);
        Expect.True(frozen.Value.Failures.Count > 0);
    }

    /// <summary>A freeze is write-once: the owner's freeze of a hash a machine froze first is not recorded, and says so.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_freeze_keeps_the_first_freezer()
    {
        var packets = Create();
        _ = await packets.Freeze(Good, PacketFreezer.Machine, Token);

        var again = await packets.Freeze(Good, PacketFreezer.Owner, Token);

        Expect.True(again.Value.Frozen);
        Expect.True(again.Value.AlreadyFrozen);
        Expect.Equal(PacketFreezer.Machine, again.Value.By);
        Expect.Equal(PacketFreezer.Machine, again.Value.AlreadyFrozenBy);
    }
}
