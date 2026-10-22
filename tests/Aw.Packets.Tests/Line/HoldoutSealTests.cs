using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Values;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Line;

/// <summary>PKT-HOLD-003 (LN-PKT4): a phase packet's holdout set hash matches its seal.</summary>
public sealed class HoldoutSealTests
{
    private static async Task<CheckResult> Check(Dictionary<string, byte[]> files, string? sealedHash)
    {
        var inspector = new LinePacketInspector(
            new InMemoryLinePacketSource().With("pkt", files),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles),
            PacketFiles.Json);
        var inspection = await inspector.Inspect(PacketKinds.Phase, "pkt", "repo", new LineInspectOptions(Sizes: null, sealedHash is null ? null : PacketHash.From(sealedHash)), CancellationToken.None);
        return Assert.Single(inspection.Value.Checks, static c => c.Rule == PacketRules.HoldoutSealed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_set_hash_the_seal_names_passes()
    {
        var result = await Check(A.LinePacket(PacketKinds.Phase).Build(), LinePacketBuilder.Hash);

        Assert.Equal(CheckState.Pass, result.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_set_hash_the_seal_does_not_name_fails_naming_both()
    {
        var result = await Check(A.LinePacket(PacketKinds.Phase).Build(), LinePacketBuilder.OtherHash);

        Assert.Equal(CheckState.Fail, result.State);
        Assert.Contains(LinePacketBuilder.Hash, result.Message, StringComparison.Ordinal);
        Assert.Contains(LinePacketBuilder.OtherHash, result.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_seal_the_rule_is_unchecked_and_says_why()
    {
        var result = await Check(A.LinePacket(PacketKinds.Phase).Build(), sealedHash: null);

        Assert.Equal(CheckState.Unchecked, result.State);
        Assert.Contains("--seal", result.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_without_a_readable_holdout_file_is_unchecked()
    {
        var result = await Check(A.LinePacket(PacketKinds.Phase).Without("holdout.json").Build(), LinePacketBuilder.Hash);

        Assert.Equal(CheckState.Unchecked, result.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_a_phase_packet_has_the_rule()
    {
        var inspector = new LinePacketInspector(
            new InMemoryLinePacketSource().With("pkt", A.LinePacket(PacketKinds.Plan).Build()),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles),
            PacketFiles.Json);

        var inspection = await inspector.Inspect(PacketKinds.Plan, "pkt", "repo", new LineInspectOptions(Sizes: null, PacketHash.From(LinePacketBuilder.Hash)), CancellationToken.None);

        Assert.DoesNotContain(inspection.Value.Checks, static c => c.Rule == PacketRules.HoldoutSealed);
    }
}
