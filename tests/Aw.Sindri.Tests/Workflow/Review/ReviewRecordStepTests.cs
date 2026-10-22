using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Review;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Review;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Aw.Sindri.Tests.Workflow.Prove;

namespace Aw.Sindri.Tests.Workflow.Review;

/// <summary>'step.review.record' on the block runtime (line spec §7.4, DTC4): the review result packet in the run area and its machine freeze.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewRecordStepTests
{
    private const string Head = "abcabcabcabcabcabc";
    private const string Address = "T-1/review/1";
    private const string Dir = "/packets/p1";
    private const string AcceptanceText = """{"schemaVersion":1,"parent":"sha256:aa","items":[{"id":"AC-1","text":"one","status":"failing","evidence":[]}]}""";
    private const string Rounds = """[{"round":1,"lens":"correctness","findings":[{"id":"f1","severity":"blocking","text":"b"}],"closes":true},{"round":2,"lens":"correctness","findings":[],"closes":true}]""";

    private static readonly string _phaseHash = "sha256:" + new string('b', 64);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public InMemoryBriefFiles Files { get; } = new InMemoryBriefFiles()
            .With("acceptance.json", AcceptanceText)
            .With("packet.json", """{"schemaVersion":1,"kind":"phase","id":"line-p1","repo":"owner/name","baseSha":"1111111111111111111111111111111111111111","status":"frozen","hash":null,"links":{}}""");

        public InMemoryRunArea Area { get; } = new();

        public InMemoryPackets Packets { get; } = new();

        public InMemoryRepoCheckouts Checkouts { get; } = new InMemoryRepoCheckouts().With("name", "/checkouts/name");

        public ReviewRecordStep Step() => new(Files, Area, Packets, Checkouts);
    }

    private static Dictionary<string, string> Facts(string verdict = "clean", string counts = "\"blocking\":0,\"issue\":0") => new(StringComparer.Ordinal)
    {
        ["change"] = $$"""{"repo":"r","branch":"b","base":"m","head":"{{Head}}"}""",
        ["phasePacket"] = $$"""{"hash":"{{_phaseHash}}","dir":"{{Dir}}"}""",
        ["review"] = $$"""{"round":2,"verdict":"{{verdict}}",{{counts}},"findings":[]}""",
        ["lensRounds"] = Rounds,
    };

    private static StepContext Context(Dictionary<string, string> facts, bool address = true, bool repo = true) =>
        BuildShipContexts.Of("step.review.record", facts) with { Address = address ? Address : null, Repo = repo ? RepoName.From("name") : null };

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    [Fact]
    public async Task A_clean_review_writes_the_packet_to_the_run_area_and_machine_freezes_it()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(Facts()), Token);

        Assert.Equal("recorded", result.Result!.Outcome.Value);
        var fact = Out(result, "review");
        foreach (var name in ReviewPack.FileNames)
        {
            Assert.NotEmpty((await rig.Area.Read(Address, "review-pack/" + name, Token)).Value);
        }

        var (_, packet, by) = Assert.Single(rig.Packets.Calls, static c => c.Verb == "freeze");
        Assert.Equal((PacketKind.Review, PacketFreezer.Machine, "/checkouts/name"), (packet.Kind, by, packet.Repo));
        Assert.Equal(packet.Directory, (string?)fact["dir"]);
        Assert.Equal(InMemoryPackets.HashOf(packet), (string?)fact["hash"]);
        Assert.Equal(Head, (string?)fact["head"]);   // step.ship.freeze compares it (SHIP-SHA-001)
        Assert.Equal(["dir", "hash", "head"], fact.AsObject().Select(static p => p.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_packet_names_the_reviewed_head_the_phase_packet_as_parent_and_its_acceptance_hash()
    {
        var rig = new Rig();

        _ = await rig.Step().Run(Context(Facts()), Token);

        var review = JsonNode.Parse((await rig.Area.Read(Address, "review-pack/review.json", Token)).Value)!;
        var index = JsonNode.Parse((await rig.Area.Read(Address, "review-pack/index.json", Token)).Value)!;
        var packet = JsonNode.Parse((await rig.Area.Read(Address, "review-pack/packet.json", Token)).Value)!;
        Assert.Equal((Head, "clean"), ((string?)review["head"], (string?)review["verdict"]));
        Assert.Equal(2, review["rounds"]!.AsArray().Count);
        Assert.Equal([_phaseHash], index["parents"]!.AsArray().Select(static p => (string?)p), StringComparer.Ordinal);
        Assert.Equal(ProofPack.Sha256(AcceptanceText), (string?)index["acceptance"]!["extends"]);
        Assert.Equal(("line-p1-review", "owner/name"), ((string?)packet["id"], (string?)packet["repo"]));
    }

    [Fact]
    public async Task A_behaviour_wrong_review_is_recorded_and_frozen_as_recordedWrong()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(Facts("behaviourWrong", "\"blocking\":1,\"issue\":0")), Token);

        Assert.Equal("recordedWrong", result.Result!.Outcome.Value);
        _ = Assert.Single(rig.Packets.Calls, static c => c.Verb == "freeze");
        Assert.Equal("behaviourWrong", (string?)JsonNode.Parse((await rig.Area.Read(Address, "review-pack/review.json", Token)).Value)!["verdict"]);
    }

    [Fact]
    public async Task A_review_whose_verdict_is_findings_is_not_recorded()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(Facts("findings", "\"blocking\":1,\"issue\":0")), Token);

        Assert.Null(result.Result);
        Assert.Contains("findings", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Packets.Calls);
        Assert.Equal(string.Empty, (await rig.Area.Read(Address, "review-pack/review.json", Token)).Value);
    }

    [Fact]
    public async Task A_clean_verdict_with_a_blocking_finding_left_is_not_recorded()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(Facts("clean", "\"blocking\":1,\"issue\":0")), Token);

        Assert.Null(result.Result);
        Assert.Empty(rig.Packets.Calls);
    }

    [Fact]
    public async Task A_packet_that_aw_packets_refuses_fails_the_Step_with_its_failures_and_freezes_nothing()
    {
        var rig = new Rig();
        _ = rig.Packets.Fails("/run/T-1/review/1/review-pack", new PacketFailure("PKT-REV-005", "packet.json", "links name a file the kind does not list", "name only review.json and index.json"));

        var result = await rig.Step().Run(Context(Facts()), Token);

        Assert.Null(result.Result);
        Assert.Contains("PKT-REV-005", result.Failure, StringComparison.Ordinal);
        Assert.Contains("links name a file the kind does not list", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_relative_packet_dir_is_read_under_the_repos_checkout_and_an_absolute_one_as_it_is()
    {
        var relative = new RootRecordingFiles(new Rig().Files);
        var absolute = new RootRecordingFiles(new Rig().Files);
        var facts = Facts();
        facts["phasePacket"] = $$"""{"hash":"{{_phaseHash}}","dir":"packets/p1"}""";
        var rig = new Rig();

        var fromRelative = await new ReviewRecordStep(relative, rig.Area, rig.Packets, rig.Checkouts).Run(Context(facts), Token);
        var fromAbsolute = await new ReviewRecordStep(absolute, new Rig().Area, new Rig().Packets, rig.Checkouts).Run(Context(Facts()), Token);

        Assert.Equal("recorded", fromRelative.Result!.Outcome.Value);
        Assert.Equal(["/checkouts/name/packets/p1"], relative.Roots.Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal("recorded", fromAbsolute.Result!.Outcome.Value);
        Assert.Equal([Dir], absolute.Roots.Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_run_area_that_cannot_be_written_fails_the_Step()
    {
        var rig = new Rig();
        rig.Area.Unwritable = true;

        var result = await rig.Step().Run(Context(Facts()), Token);

        Assert.Null(result.Result);
        Assert.Empty(rig.Packets.Calls);
    }

    [Fact]
    public async Task A_packet_tool_that_cannot_run_fails_the_Step()
    {
        var rig = new Rig();
        _ = rig.Packets.Unreadable("/run/T-1/review/1/review-pack");

        var result = await rig.Step().Run(Context(Facts()), Token);

        Assert.Null(result.Result);
        Assert.Contains("aw-packets", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Step_without_an_address_a_repo_or_a_checkout_fails_and_says_which()
    {
        var rig = new Rig();

        Assert.Contains("address", (await rig.Step().Run(Context(Facts(), address: false), Token)).Failure, StringComparison.Ordinal);
        Assert.Contains("repo", (await rig.Step().Run(Context(Facts(), repo: false), Token)).Failure, StringComparison.Ordinal);
        var bare = new Rig();
        var noCheckout = new ReviewRecordStep(bare.Files, bare.Area, bare.Packets, new InMemoryRepoCheckouts());
        Assert.Contains("checkout", (await noCheckout.Run(Context(Facts()), Token)).Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("change")]
    [InlineData("phasePacket")]
    [InlineData("review")]
    public async Task A_Step_missing_a_fact_it_needs_fails_and_names_the_Step(string missing)
    {
        var facts = Facts();
        _ = facts.Remove(missing);

        var result = await new Rig().Step().Run(Context(facts), Token);

        Assert.Null(result.Result);
        Assert.Contains("step.review.record", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_phase_packet_that_cannot_be_read_fails_the_Step()
    {
        var rig = new Rig();
        var step = new ReviewRecordStep(new InMemoryBriefFiles(), rig.Area, rig.Packets, rig.Checkouts);

        var result = await step.Run(Context(Facts()), Token);

        Assert.Null(result.Result);
        Assert.Contains(Dir, result.Failure, StringComparison.Ordinal);
    }
}
