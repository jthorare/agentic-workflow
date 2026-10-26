using System.Text;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Scoping;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Scoping;

public sealed class ScopingDigestTests
{
    private static async Task<ScopingFiles> Read(Dictionary<string, byte[]> files) =>
        (await new InMemoryScopingPacketSource().With("pkt", files).Read("pkt", CancellationToken.None)).Value;

    private static async Task<string> Hash(Dictionary<string, byte[]> files) => PacketDigest.Compute(await Read(files)).Value.Value;

    [Fact, Trait("Lane", "unit")]
    public async Task The_digest_of_the_gold_scope_packet_is_pinned() =>
        // digestVersion 1: a change to these bytes needs a new PacketDigest.Version, not a new pin.
        Assert.Equal("sha256:3d63d7f160cfc9ab3c7b40ada87f1352f6899561f51a800627b12e5dbef7ba4f", await Hash(A.ScopingPacket().Build()), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task A_json_file_hashes_by_content_not_by_formatting_key_order_or_schema_hint()
    {
        var plain = await Hash(A.ScopingPacket().With("size.json", "{\"a\":1,\"b\":[true]}").Build());
        var reformatted = await Hash(A.ScopingPacket().With("size.json", "{ \"$schema\": \"x\",\n \"b\": [ true ], \"a\": 1 }").Build());
        var changed = await Hash(A.ScopingPacket().With("size.json", "{\"a\":2,\"b\":[true]}").Build());

        Assert.Equal(plain, reformatted, StringComparer.Ordinal);
        Assert.NotEqual(plain, changed, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_jsonl_or_text_file_hashes_by_its_bytes()
    {
        var plain = await Hash(A.ScopingPacket().Build());

        Assert.NotEqual(plain, await Hash(A.ScopingPacket().With("answers.jsonl", "{ \"round\": 1 }\n{ \"round\": 2 }").Build()), StringComparer.Ordinal);
        Assert.NotEqual(plain, await Hash(A.ScopingPacket().With("brief.md", "# Brief \n").Build()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Entries_the_kind_does_not_list_are_not_hashed() =>
        Assert.Equal(await Hash(A.ScopingPacket().Build()), await Hash(A.ScopingPacket().With("notes.txt", "x").Build()), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_file_has_no_hash()
    {
        var hash = PacketDigest.Compute(await Read(A.ScopingPacket().Without("runs.json").Build()));

        Assert.Equal("AW-PACKETS-003", hash.Error.Code);
        Assert.Contains("runs.json", hash.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_duplicate_key_has_no_canonical_form()
    {
        var hash = PacketDigest.Compute(await Read(A.ScopingPacket().With("runs.json", Encoding.UTF8.GetBytes("{\"a\":1,\"a\":2}")).Build()));

        Assert.Equal("AW-PACKETS-003", hash.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_registry_lists_every_kind_by_name()
    {
        Assert.Equal(["build", "scoping", "design", "plan", "phase", "proof", "review", "handoff"], PacketKinds.All.Select(static k => k.Name), StringComparer.Ordinal);
        Assert.True(PacketKinds.Build.IsManifestDriven);
        Assert.False(PacketKinds.Scoping.IsManifestDriven);
        Assert.Equal(14, PacketKinds.Scoping.Files.Count);
        Assert.Null(PacketKinds.Find("Scoping"));
        Assert.Same(PacketKinds.Scoping, PacketKinds.Find("scoping"));
    }
}
