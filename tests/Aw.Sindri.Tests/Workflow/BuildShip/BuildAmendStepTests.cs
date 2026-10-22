using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>'step.build.amend' on the block runtime (line spec §7.2, LN-PKT1, LN-PKT5): a scope grant is a new child phase packet, owner-frozen, and the packet in force.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildAmendStepTests
{
    private const string Address = "T-1/build/1";
    private const string ParentDir = "/packets/p1";
    private const string ParentHash = "sha256:0000000000000000000000000000000000000000000000000000000000000001";

    private readonly InMemoryPackets _packets = new();
    private readonly InMemoryPacketDirectory _files = new();
    private readonly InMemoryRunArea _area = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> ParentFiles(string id = "fixture-p1") => new(StringComparer.Ordinal)
    {
        ["packet.json"] = $$$"""{"schemaVersion":1,"kind":"phase","id":"{{{id}}}","repo":"o/n","baseSha":"3288642cd47359fff2642b8bf429976915ca4894","status":"frozen","hash":"{{{ParentHash}}}","links":{"phase":"phase.json","index":"index.json"}}""",
        ["phase.json"] = """{"schemaVersion":1,"goal":"g","kind":"feature","deps":[],"size":"S","base":{"rule":"default-branch","ref":"main"},"allowedFiles":["src/**"],"testChange":[],"readSeeds":[],"budget":{}}""",
        ["index.json"] = """{"schemaVersion":1,"kind":"phase","parents":["sha256:aa"],"acceptance":{"self":"sha256:bb","extends":null}}""",
        ["acceptance.json"] = """{"schemaVersion":1,"parent":null,"items":[]}""",
    };

    private BuildAmendStep Step() => new(_packets, _files, _area, _clock);

    private StepContext Context(string answers = """{"allow":["docs/**","new: src/x.cs"],"reason":"needs docs"}""", string id = "fixture-p1")
    {
        _ = _files.Put(ParentDir, ParentFiles(id));
        return BuildShipContexts.Of("step.build.amend", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["activePacket"] = $$"""{"hash":"{{ParentHash}}","dir":"{{ParentDir}}","repo":"/work/p1"}""",
            ["answers"] = answers,
        }, Address);
    }

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private async Task<JsonNode> Child(string name) => JsonNode.Parse((await _files.Read(InMemoryPacketDirectory.DirectoryOf(Address, "packets/" + name), Token)).Value["phase.json"])!;

    [Fact]
    public async Task An_allow_writes_a_child_packet_with_the_grant_and_owner_freezes_it()
    {
        var result = await Step().Run(Context(), Token);

        Assert.Equal("amended", result.Result!.Outcome.Value);
        var dir = InMemoryPacketDirectory.DirectoryOf(Address, "packets/fixture-p1-a1");
        var (_, packet, by) = Assert.Single(_packets.Calls, static c => string.Equals(c.Verb, "freeze", StringComparison.Ordinal));
        Assert.Equal((PacketKind.Phase, dir, "/work/p1", PacketFreezer.Owner), (packet.Kind, packet.Directory, packet.Repo, by));
        var child = await Child("fixture-p1-a1");
        Assert.Equal(["src/**", "docs/**", "new: src/x.cs"], child["allowedFiles"]!.AsArray().Select(static n => (string?)n), StringComparer.Ordinal);
        Assert.Equal(ParentHash, (string?)child["amends"]!["parent"], StringComparer.Ordinal);
        Assert.Equal("needs docs", (string?)child["amends"]!["reason"], StringComparer.Ordinal);
        Assert.Equal(["docs/**", "new: src/x.cs"], child["amends"]!["paths"]!.AsArray().Select(static n => (string?)n), StringComparer.Ordinal);
        Assert.Equal(dir, (string?)Out(result, "activePacket")["dir"], StringComparer.Ordinal);
        Assert.Equal(InMemoryPackets.HashOf(packet), (string?)Out(result, "activePacket")["hash"], StringComparer.Ordinal);
        Assert.Equal("/work/p1", (string?)Out(result, "activePacket")["repo"], StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_child_is_a_draft_with_a_new_id_and_the_parent_as_its_only_parent_and_the_rest_copied()
    {
        _ = await Step().Run(Context(), Token);

        var written = (await _files.Read(InMemoryPacketDirectory.DirectoryOf(Address, "packets/fixture-p1-a1"), Token)).Value;
        var packet = JsonNode.Parse(written["packet.json"])!;
        Assert.Equal(("fixture-p1-a1", "draft", null), ((string?)packet["id"], (string?)packet["status"], (string?)packet["hash"]));
        Assert.Equal("3288642cd47359fff2642b8bf429976915ca4894", (string?)packet["baseSha"], StringComparer.Ordinal);
        Assert.Equal([ParentHash], JsonNode.Parse(written["index.json"])!["parents"]!.AsArray().Select(static n => (string?)n), StringComparer.Ordinal);
        Assert.Equal("sha256:bb", (string?)JsonNode.Parse(written["index.json"])!["acceptance"]!["self"], StringComparer.Ordinal);
        Assert.Equal(ParentFiles()["acceptance.json"], written["acceptance.json"], StringComparer.Ordinal);
    }

    [Fact]
    public async Task Amending_an_amendment_takes_the_next_number()
    {
        _ = await Step().Run(Context(id: "fixture-p1-a1"), Token);

        Assert.Equal("fixture-p1-a2", (string?)JsonNode.Parse((await _files.Read(InMemoryPacketDirectory.DirectoryOf(Address, "packets/fixture-p1-a2"), Token)).Value["packet.json"])!["id"], StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_grant_with_no_reason_takes_the_stock_one_and_a_stamp_records_when()
    {
        _ = await Step().Run(Context("""{"allow":["b/**"],"revert":["c.cs"]}"""), Token);

        var child = await Child("fixture-p1-a1");
        Assert.Equal(["src/**", "b/**"], child["allowedFiles"]!.AsArray().Select(static n => (string?)n), StringComparer.Ordinal);
        Assert.Equal(ScopeGrant.DefaultReason, (string?)child["amends"]!["reason"], StringComparer.Ordinal);
        Assert.StartsWith("2026-10-19T09:00:00", (string?)child["amends"]!["at"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_escape_is_recorded_in_the_run_area()
    {
        _ = await Step().Run(Context(), Token);

        var line = JsonNode.Parse((await _area.Read(Address, "escapes.jsonl", Token)).Value.Trim())!;
        Assert.Equal(("build", "amend", "fixture-p1-a1"), ((string?)line["from"], (string?)line["station"], (string?)line["id"]));
    }

    [Fact]
    public async Task A_packet_that_fails_its_checks_fails_the_Step_and_names_the_rule()
    {
        var context = Context();
        _ = _packets.Fails(InMemoryPacketDirectory.DirectoryOf(Address, "packets/fixture-p1-a1"), new PacketFailure("PKT-PHS-009", "packet", "amends is empty", Fix: null));

        var result = await Step().Run(context, Token);

        Assert.Null(result.Result);
        Assert.Contains("PKT-PHS-009", result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"revert":["a"]}""")]
    [InlineData("""{"allow":[],"reason":"x"}""")]
    [InlineData("""{"allow":[" "]}""")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task Answers_with_no_usable_grant_fail_the_Step(string answers)
    {
        var result = await Step().Run(Context(answers), Token);

        Assert.Null(result.Result);
        Assert.Empty(_packets.Calls);
    }

    [Fact]
    public async Task An_unreadable_parent_fails_the_Step()
    {
        var context = BuildShipContexts.Of("step.build.amend", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["activePacket"] = """{"hash":"sha256:aa","dir":"/nowhere","repo":"/work/p1"}""",
            ["answers"] = """{"allow":["a"],"reason":"r"}""",
        }, Address);

        var result = await Step().Run(context, Token);

        Assert.Contains("/nowhere", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_packet_with_no_repo_checkout_fails_the_Step_before_anything_is_written()
    {
        var context = Context();
        var noRepo = BuildShipContexts.Of("step.build.amend", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["activePacket"] = $$"""{"hash":"{{ParentHash}}","dir":"{{ParentDir}}"}""",
            ["answers"] = """{"allow":["a"],"reason":"r"}""",
        }, Address);
        _ = context;

        var result = await Step().Run(noRepo, Token);

        Assert.Contains("repo", result.Failure, StringComparison.Ordinal);
        Assert.Empty(_packets.Calls);
    }
}
