using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Design;

/// <summary>The design packet assembler (line spec §3.2 row 2, §4.4): the gold design's own files assemble back into the gold packet, and the fields code owns are stamped by code.</summary>
public sealed class DesignPacketTests
{
    private static readonly string[] _files =
    [
        "packet.json", "index.json", "spec.md", "sections.json", "coverage.json", "decisions.json", "acceptance.json", "signals.json",
        "contracts.json", "lens-rounds.jsonl", "style.json", "escapes.json", "runs.json",
    ];

    private static readonly PacketIdentity _identity = new("design-expiry", "fixture-labs/fixture-notes", "3288642cd47359fff2642b8bf429976915ca4894");
    private const string ScopeHash = "sha256:48b9dc81f8935c81f465f785a6e7dba0abd9697480786ad7994d1475da00c973";

    private static Dictionary<string, string> Built(LineFixture facts) => DesignPacket.Build(new LineFacts(facts), _identity, ScopeHash).ToDictionary(static f => f.Path, static f => f.Content, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_gold_design_assembles_back_into_the_gold_packet_byte_for_byte()
    {
        var gold = LineFixture.Design();

        var built = Built(gold);

        Assert.Equal(_files, DesignPacket.Build(new LineFacts(gold), _identity, ScopeHash).Select(f => f.Path), StringComparer.Ordinal);
        foreach (var name in _files)
        {
            Assert.Equal(gold[name], built[name]);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_assembled_packet_completes_the_evidence_checks()
    {
        var facts = LineFixture.Design();
        foreach (var (name, content) in Built(facts))
        {
            facts[name] = content;
        }

        var response = await DesignCheckTests.Run(facts, LineStage.Evidence);

        Assert.Equal(LineOutcome.Complete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Index_json_carries_the_scope_hash_the_acceptance_files_hash_and_the_scope_acceptance_hash()
    {
        var facts = LineFixture.Design();

        var index = JsonNode.Parse(Built(facts)["index.json"])!;

        Assert.Equal("design", (string?)index["kind"]);
        Assert.Equal([ScopeHash], index["parents"]!.AsArray().Select(p => (string?)p), StringComparer.Ordinal);
        Assert.Equal(Sha(Built(facts)["acceptance.json"]), (string?)index["acceptance"]!["self"]);
        Assert.Equal(Sha(facts["scope/acceptance.json"]), (string?)index["acceptance"]!["extends"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Packet_json_names_each_file_and_the_identity()
    {
        var packet = JsonNode.Parse(Built(LineFixture.Design())["packet.json"])!;

        Assert.Equal("design-expiry", (string?)packet["id"]);
        Assert.Equal("fixture-labs/fixture-notes", (string?)packet["repo"]);
        Assert.Equal("draft", (string?)packet["status"]);
        Assert.Null(packet["hash"]);
        Assert.Equal(_files.Where(f => f != "packet.json").Order(StringComparer.Ordinal), packet["links"]!.AsObject().Select(l => (string?)l.Value).Order(StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_status_the_writer_flipped_is_set_back_to_failing_with_no_evidence_and_the_parent_is_the_scope_acceptance_hash()
    {
        var facts = LineFixture.Design().Edit("acceptance.json", n =>
        {
            var item = n["items"]![0]!;
            item["status"] = "passing";
            item["evidence"] = new JsonArray(new JsonObject { ["kind"] = "test" });
            n["parent"] = null;
        });

        var acceptance = JsonNode.Parse(Built(facts)["acceptance.json"])!;

        Assert.Equal("failing", (string?)acceptance["items"]![0]!["status"]);
        Assert.Empty(acceptance["items"]![0]!["evidence"]!.AsArray());
        Assert.Equal(Sha(facts["scope/acceptance.json"]), (string?)acceptance["parent"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_item_the_writer_added_is_untrusted_and_a_scope_item_keeps_the_trust_the_scope_gave_it()
    {
        var facts = LineFixture.Design().Edit("acceptance.json", n =>
        {
            n["items"]![10]!["trust"] = "owner";
            n["items"]![5]!["trust"] = "untrusted";
        });

        var acceptance = JsonNode.Parse(Built(facts)["acceptance.json"])!;

        Assert.Equal("untrusted", (string?)acceptance["items"]![10]!["trust"]);
        Assert.Equal("owner", (string?)acceptance["items"]![5]!["trust"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_escapes_are_derived_by_code_from_what_the_design_adds()
    {
        var facts = LineFixture.Design().Without("escapes.json");

        var escapes = JsonNode.Parse(Built(facts)["escapes.json"])!["escapes"]!.AsArray();

        Assert.Equal(["acceptance:AC-11", "surface:surface:route/export-download"], escapes.Select(e => $"{(string?)e!["kind"]}:{(string?)e["id"]}"), StringComparer.Ordinal);
        Assert.All(escapes, e => { Assert.Equal("scoping", (string?)e!["from"]); Assert.Equal("design", (string?)e["station"]); });
    }

    [Fact, Trait("Lane", "unit")]
    public void An_escape_the_writer_explained_keeps_its_reason()
    {
        var built = JsonNode.Parse(Built(LineFixture.Design())["escapes.json"])!["escapes"]!.AsArray();

        Assert.Equal("Export regression, added by the design; export is excluded in the scope map.", (string?)built[0]!["why"]);
    }

    private static string Sha(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
