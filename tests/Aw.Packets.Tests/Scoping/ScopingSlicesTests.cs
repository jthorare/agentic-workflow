using Aw.Packets.App.Commands;
using Aw.Packets.App.Features.FreezeScopingPacket;
using Aw.Packets.App.Features.HashPacket;
using Aw.Packets.App.Features.ValidateScopingPacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Hashing;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Packets.Tests.Scoping;

public sealed class ScopingSlicesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static InMemoryScopingPacketSource Source(Dictionary<string, byte[]> files) => new InMemoryScopingPacketSource().With("pkt", files);

    private static FreezeScopingPacketService Freezer(Dictionary<string, byte[]> files, InMemoryFreezeLog log) =>
        new(Source(files), log, new FakeTimeProvider(_now), PacketFiles.Json);

    private static FreezeScopingPacketRequest Request => new("pkt", "example/repo", "idea-1");

    [Fact, Trait("Lane", "unit")]
    public async Task ValidateScopingPacket_reports_every_rule_and_passes_the_gold_packet()
    {
        var service = new ValidateScopingPacketService(Source(A.ScopingPacket().Build()), PacketFiles.Json);

        var result = await service.Handle(new ValidateScopingPacketRequest("pkt"), CancellationToken.None);

        Assert.True(result.Value.Passed);
        Assert.Equal((1, "scoping", 5), (result.Value.SchemaVersion, result.Value.Kind, result.Value.Checks.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ValidateScopingPacket_fails_with_the_failing_rule_and_a_missing_directory_is_AW_PACKETS_002()
    {
        var failing = await new ValidateScopingPacketService(Source(A.ScopingPacket().Without("brief.md").Build()), PacketFiles.Json)
            .Handle(new ValidateScopingPacketRequest("pkt"), CancellationToken.None);
        var missing = await new ValidateScopingPacketService(Source(A.ScopingPacket().Build()), PacketFiles.Json)
            .Handle(new ValidateScopingPacketRequest("elsewhere"), CancellationToken.None);

        Assert.False(failing.Value.Passed);
        Assert.Equal(PacketsErrors.NotFound.Code, missing.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task HashPacket_of_a_scope_packet_is_the_digest_freeze_records()
    {
        var files = A.ScopingPacket().Build();
        var hashed = await new HashPacketService(new PacketHasher(new InMemoryPacketSource(), Source(files), new InMemoryLinePacketSource(), PacketFiles.Json))
            .Handle(new HashPacketRequest("scoping", "pkt"), CancellationToken.None);
        var log = new InMemoryFreezeLog();
        var frozen = await Freezer(files, log).Handle(Request, CancellationToken.None);

        Assert.Equal(frozen.Value.Hash, hashed.Value.Hash);
        Assert.Equal((1, "scoping", PacketDigest.Version), (hashed.Value.SchemaVersion, hashed.Value.Kind, hashed.Value.DigestVersion));
        Assert.Equal(PacketKinds.Scoping.Files.Select(static f => f.Name).Order(StringComparer.Ordinal), hashed.Value.Files, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task HashPacket_of_a_build_packet_is_the_digest_freeze_records()
    {
        var packet = A.Packet().Build();
        var source = new InMemoryPacketSource().With("pkt", PacketFiles.Of(packet));

        var hashed = await new HashPacketService(new PacketHasher(source, new InMemoryScopingPacketSource(), new InMemoryLinePacketSource(), PacketFiles.Json)).Handle(new HashPacketRequest("build", "pkt"), CancellationToken.None);

        var loaded = await source.Load("pkt", CancellationToken.None);
        Assert.Equal(PacketDigest.Compute(loaded.Value).Value, hashed.Value.Hash);
        Assert.Contains("packet.json", hashed.Value.Files, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task HashPacket_errors_name_the_missing_directory_or_file()
    {
        var service = new HashPacketService(new PacketHasher(new InMemoryPacketSource(), Source(A.ScopingPacket().Without("runs.json").Build()), new InMemoryLinePacketSource(), PacketFiles.Json));

        var noFile = await service.Handle(new HashPacketRequest("scoping", "pkt"), CancellationToken.None);
        var noDirectory = await service.Handle(new HashPacketRequest("scoping", "nowhere"), CancellationToken.None);
        var noBuild = await service.Handle(new HashPacketRequest("build", "nowhere"), CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, noFile.Error.Code);
        Assert.Equal(PacketsErrors.NotFound.Code, noDirectory.Error.Code);
        Assert.Equal(PacketsErrors.NotFound.Code, noBuild.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "scoping")]
    [InlineData("pkt", "other")]
    public void HashPacket_refuses_an_empty_directory_and_an_unknown_kind(string directory, string kind) =>
        Assert.Equal(PacketsErrors.Invalid.Code, new HashPacketValidator().Validate(new HashPacketRequest(kind, directory)).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public async Task FreezeScopingPacket_records_the_hash_under_the_instance_with_no_base_commit()
    {
        var log = new InMemoryFreezeLog();

        var result = await Freezer(A.ScopingPacket().Build(), log).Handle(Request, CancellationToken.None);

        var frozen = result.Value;
        Assert.True(frozen.Frozen);
        Assert.False(frozen.AlreadyFrozen);
        var hash = Assert.NotNull(frozen.Hash);
        Assert.Equal($"example/repo/idea-1/frozen/{hash.Hex}.json", frozen.Location);
        var record = Assert.Single(log.Records).Value;
        Assert.Equal(("scoping", "idea-1", _now, 1), (record.Kind, record.Packet.Value, record.FrozenAt, record.DigestVersion));
        Assert.Null(record.BaseSha);
        Assert.Equal(14, record.Files.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Freezing_a_scope_packet_again_is_already_frozen()
    {
        var log = new InMemoryFreezeLog();
        var service = Freezer(A.ScopingPacket().Build(), log);

        _ = await service.Handle(Request, CancellationToken.None);
        var again = await service.Handle(Request, CancellationToken.None);

        Assert.True(again.Value.AlreadyFrozen);
        _ = Assert.Single(log.Records);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_packet_that_fails_a_check_is_not_frozen_and_nothing_is_written()
    {
        var log = new InMemoryFreezeLog();

        var result = await Freezer(A.ScopingPacket().With("notes.txt", "x").Build(), log).Handle(Request, CancellationToken.None);

        Assert.False(result.Value.Frozen);
        Assert.Null(result.Value.Hash);
        Assert.Contains(result.Value.Checks, static c => c.State == CheckState.Fail);
        Assert.Empty(log.Records);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unwritable_freeze_log_is_AW_PACKETS_007()
    {
        var result = await Freezer(A.ScopingPacket().Build(), new InMemoryFreezeLog { Unwritable = true }).Handle(Request, CancellationToken.None);

        Assert.Equal(PacketsErrors.FreezeLogUnavailable.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "example/repo", "idea-1")]
    [InlineData("pkt", "not-a-slug", "idea-1")]
    [InlineData("pkt", "example/repo", "Idea 1")]
    [InlineData("pkt", "example/repo", "../x")]
    public void FreezeScopingPacket_refuses_a_bad_directory_slug_or_instance(string directory, string slug, string instance) =>
        Assert.Equal(PacketsErrors.Invalid.Code, new FreezeScopingPacketValidator().Validate(new FreezeScopingPacketRequest(directory, slug, instance)).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void ValidateScopingPacket_refuses_an_empty_directory() =>
        Assert.Equal(PacketsErrors.Invalid.Code, new ValidateScopingPacketValidator().Validate(new ValidateScopingPacketRequest(" ")).Error.Code);

    [Theory, Trait("Lane", "unit")]
    [InlineData("validate dir", "build", "dir")]
    [InlineData("dir --kind scoping --json", "scoping", "dir --json")]
    [InlineData("--kind build dir", "build", "dir")]
    public void The_kind_argument_is_pulled_out_wherever_it_sits(string args, string kind, string rest)
    {
        var parsed = KindArgument.Extract([.. args.Split(' ').Skip(args.StartsWith("validate", StringComparison.Ordinal) ? 1 : 0)]);

        Assert.Equal((kind, rest), (parsed!.Value.Kind.Name, string.Join(' ', parsed.Value.Arguments)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--kind")]
    [InlineData("--kind other")]
    [InlineData("--kind scoping --kind build")]
    public void A_missing_unknown_or_repeated_kind_is_refused(string args) =>
        Assert.Null(KindArgument.Extract(args.Split(' ')));

    [Fact, Trait("Lane", "unit")]
    public void Simple_arguments_take_a_directory_json_and_the_named_value_flags_once()
    {
        var parsed = SimpleArguments.Parse(["--instance", "i1", "dir", "--json"], "--instance", "--repo-slug");

        Assert.Equal(("dir", true, "i1"), (parsed!.PacketDirectory, parsed.Json, parsed.Values["--instance"]));
        Assert.Null(SimpleArguments.Parse(["dir", "--instance"], "--instance"));
        Assert.Null(SimpleArguments.Parse(["dir", "--instance", "a", "--instance", "b"], "--instance"));
        Assert.Null(SimpleArguments.Parse(["dir", "other"]));
        Assert.Null(SimpleArguments.Parse(["--repo", "x", "dir"]));
        Assert.Null(SimpleArguments.Parse(["--json"]));
    }
}
