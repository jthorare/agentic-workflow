using Aw.Sindri.Core.Packets;
using Xunit;

namespace Aw.Sindri.Testing.Packets;

/// <summary>The contract every <see cref="IFrozenPackets"/> honours: a hash that was frozen is found with its record, any other is not found, and a malformed hash is refused before it touches storage.</summary>
public abstract class FrozenPacketsContract
{
    /// <summary>The hash of a design packet the owner froze.</summary>
    protected const string DesignHash = "sha256:3deb049307bf38e0981ed4ed374f7d84a63f305f0067a247c10f839dfd4f87e4";

    /// <summary>The hash of a scope packet a Step froze.</summary>
    protected const string ScopeHash = "sha256:48b9dc81f8935c81f465f785a6e7dba0abd9697480786ad7994d1475da00c973";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Records that hold exactly <paramref name="held"/>.</summary>
    /// <param name="held">The freeze records.</param>
    /// <returns>The port.</returns>
    protected abstract IFrozenPackets Create(IReadOnlyList<FrozenPacket> held);

    private IFrozenPackets Holding() => Create([new FrozenPacket(DesignHash, "design-expiry", "design", PacketFreezer.Owner), new FrozenPacket(ScopeHash, "scope-expiry", "scoping", PacketFreezer.Machine)]);

    /// <summary>A frozen hash is found with its packet, kind and freezer.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_frozen_hash_is_found_with_its_record()
    {
        var found = await Holding().Find(DesignHash, Token);

        Expect.True(found.IsSuccess);
        Expect.Equal(new FrozenPacket(DesignHash, "design-expiry", "design", PacketFreezer.Owner), found.Value);
    }

    /// <summary>The freezer of a record is read back, owner or machine.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_freezer_is_read_back()
    {
        var found = await Holding().Find(ScopeHash, Token);

        Expect.Equal(PacketFreezer.Machine, found.Value!.By);
    }

    /// <summary>A hash nobody froze is not found: no error, no record.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_hash_nobody_froze_is_not_found()
    {
        var found = await Holding().Find("sha256:" + new string('a', 64), Token);

        Expect.True(found.IsSuccess);
        Expect.True(found.Value is null);
    }

    /// <summary>A malformed hash is an error, never a path.</summary>
    /// <param name="hash">A hash that is not 'sha256:' and 64 lower-case hex digits.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("sha256:abc")]
    [InlineData("../../etc/passwd")]
    [InlineData("sha256:3DEB049307BF38E0981ED4ED374F7D84A63F305F0067A247C10F839DFD4F87E4")]
    public async Task A_malformed_hash_is_refused(string hash)
    {
        var found = await Holding().Find(hash, Token);

        Expect.True(found.IsError);
    }
}
