using Aw.Packets.App.Features.FreezeLinePacket;
using Aw.Packets.App.Features.HashPacket;
using Aw.Packets.App.Features.ValidateLinePacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Hashing;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Packets.Tests.Line;

public sealed class LineSlicesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static LinePacketInspector Inspector(Dictionary<string, byte[]> files) => new(
        new InMemoryLinePacketSource().With("pkt", files),
        new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles),
        PacketFiles.Json);

    private static FreezeLinePacketService Freezer(Dictionary<string, byte[]> files, InMemoryFreezeLog log) => new(Inspector(files), log, new FakeTimeProvider(_now));

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(LineChecksTests.Kinds), MemberType = typeof(LineChecksTests))]
    public async Task ValidateLinePacket_passes_the_gold_packet_and_names_its_kind_and_id(string name)
    {
        var kind = PacketKinds.Find(name)!;

        var result = await new ValidateLinePacketService(Inspector(A.LinePacket(kind).Build()), new InMemorySizesSource(), new InMemorySealSource())
            .Handle(new ValidateLinePacketRequest(name, "pkt"), CancellationToken.None);

        Assert.True(result.Value.Passed);
        Assert.Equal((1, name), (result.Value.SchemaVersion, result.Value.Kind));
        Assert.Equal(name == "phase" ? "expiry-p1" : $"expiry-{name}", result.Value.Packet?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ValidateLinePacket_fails_without_a_packet_json_and_a_missing_directory_is_AW_PACKETS_002()
    {
        var failing = await new ValidateLinePacketService(Inspector(A.LinePacket(PacketKinds.Review).Without("packet.json").Build()), new InMemorySizesSource(), new InMemorySealSource())
            .Handle(new ValidateLinePacketRequest("review", "pkt"), CancellationToken.None);
        var missing = await new ValidateLinePacketService(Inspector(A.LinePacket(PacketKinds.Review).Build()), new InMemorySizesSource(), new InMemorySealSource())
            .Handle(new ValidateLinePacketRequest("review", "elsewhere"), CancellationToken.None);

        Assert.False(failing.Value.Passed);
        Assert.Null(failing.Value.Packet);
        Assert.Equal(PacketsErrors.NotFound.Code, missing.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_validators_refuse_a_kind_that_is_not_a_line_kind_and_an_empty_directory()
    {
        var validate = new ValidateLinePacketValidator();
        var freeze = new FreezeLinePacketValidator();

        Assert.True(validate.Validate(new ValidateLinePacketRequest("design", "pkt")).IsSuccess);
        Assert.True(validate.Validate(new ValidateLinePacketRequest("build", "pkt")).IsError);
        Assert.True(validate.Validate(new ValidateLinePacketRequest("scoping", "pkt")).IsError);
        Assert.True(validate.Validate(new ValidateLinePacketRequest("design", " ")).IsError);
        Assert.True(validate.Validate(new ValidateLinePacketRequest("design", "pkt", "")).IsError);
        Assert.True(freeze.Validate(new FreezeLinePacketRequest("plan", "pkt")).IsSuccess);
        Assert.True(freeze.Validate(new FreezeLinePacketRequest("nope", "pkt")).IsError);
        Assert.True(freeze.Validate(new FreezeLinePacketRequest("plan", "")).IsError);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(LineChecksTests.Kinds), MemberType = typeof(LineChecksTests))]
    public async Task FreezeLinePacket_records_the_hash_with_the_kind_the_base_sha_and_the_files_it_covers(string name)
    {
        var kind = PacketKinds.Find(name)!;
        var files = A.LinePacket(kind).Build();
        var log = new InMemoryFreezeLog();

        var frozen = await Freezer(files, log).Handle(new FreezeLinePacketRequest(name, "pkt"), CancellationToken.None);

        var record = Assert.Single(log.Records.Values);
        Assert.True(frozen.Value.Frozen);
        Assert.Equal((name, PacketBuilder.BaseSha, "example/repo", _now), (record.Kind, record.BaseSha?.Value, record.Repo.Value, record.FrozenAt));
        Assert.Equal(frozen.Value.Hash, record.Hash);
        Assert.Equal(files.Keys.Order(StringComparer.Ordinal), record.Files, StringComparer.Ordinal);
        Assert.Equal(frozen.Value.Location, Assert.Single(log.Records.Keys));
        var again = await Freezer(files, log).Handle(new FreezeLinePacketRequest(name, "pkt"), CancellationToken.None);
        Assert.True(again.Value.AlreadyFrozen);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task FreezeLinePacket_freezes_nothing_when_a_check_fails_and_reports_a_log_that_cannot_be_written()
    {
        var log = new InMemoryFreezeLog();
        var broken = await Freezer(A.LinePacket(PacketKinds.Phase).Edit("phase.json", "\"goal\": \"Add the expiry field\"", "\"goal\": \"\"").Build(), log)
            .Handle(new FreezeLinePacketRequest("phase", "pkt"), CancellationToken.None);
        var unwritable = await Freezer(A.LinePacket(PacketKinds.Phase).Build(), new InMemoryFreezeLog { Unwritable = true })
            .Handle(new FreezeLinePacketRequest("phase", "pkt"), CancellationToken.None);

        Assert.False(broken.Value.Frozen);
        Assert.Null(broken.Value.Hash);
        Assert.Contains(broken.Value.Checks, static c => c.Rule == "PKT-PHS-008" && c.State == CheckState.Fail);
        Assert.Empty(log.Records);
        Assert.Equal(PacketsErrors.FreezeLogUnavailable.Code, unwritable.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_frozen_packet_can_be_frozen_again_after_its_manifest_records_the_freeze_because_status_and_hash_are_not_hashed()
    {
        var log = new InMemoryFreezeLog();
        var first = await Freezer(A.LinePacket(PacketKinds.Design).Build(), log).Handle(new FreezeLinePacketRequest("design", "pkt"), CancellationToken.None);
        var recorded = A.LinePacket(PacketKinds.Design).Edit("packet.json", "\"status\": \"draft\", \"hash\": null", $"\"status\": \"frozen\", \"hash\": \"{first.Value.Hash!.Value.Value}\"").Build();

        var second = await Freezer(recorded, log).Handle(new FreezeLinePacketRequest("design", "pkt"), CancellationToken.None);

        Assert.True(second.Value.AlreadyFrozen);
        Assert.Equal(first.Value.Hash, second.Value.Hash);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(LineChecksTests.Kinds), MemberType = typeof(LineChecksTests))]
    public async Task HashPacket_of_a_line_packet_is_the_digest_freeze_records_and_lists_the_files_it_covers(string name)
    {
        var kind = PacketKinds.Find(name)!;
        var files = A.LinePacket(kind).With("notes.txt", "x").Build();
        var lines = new InMemoryLinePacketSource().With("pkt", files);
        var log = new InMemoryFreezeLog();

        var hashed = await new HashPacketService(new PacketHasher(new InMemoryPacketSource(), new InMemoryScopingPacketSource(), lines, PacketFiles.Json)).Handle(new HashPacketRequest(name, "pkt"), CancellationToken.None);
        var frozen = await Freezer(A.LinePacket(kind).Build(), log).Handle(new FreezeLinePacketRequest(name, "pkt"), CancellationToken.None);

        Assert.Equal(frozen.Value.Hash, hashed.Value.Hash);
        Assert.Equal((1, name, PacketDigest.Version), (hashed.Value.SchemaVersion, hashed.Value.Kind, hashed.Value.DigestVersion));
        Assert.Equal(files.Keys.Where(static k => k != "notes.txt").Order(StringComparer.Ordinal), hashed.Value.Files, StringComparer.Ordinal);
    }
}
