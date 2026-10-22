using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.design.intake' on the block runtime (line spec §4.1): verifies the frozen scope packet, loads it into fact:scope and writes the eight seeds; fresh, or revise on a revision from stop 2.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignIntakeStepTests
{
    private static readonly string[] _seeds = ["accepted", "answers", "changedSections", "design", "findings", "lensRounds", "packs", "scope"];

    private static async Task<BlockStepResult> Run(LineStation station, string? scopePacket = null, string revision = "First pass: no revision yet.", string? prior = null)
    {
        var facts = new ScopingFacts { ["scopePacket"] = scopePacket ?? LineStation.ScopePacket, ["designRevision"] = revision };
        if (prior is not null)
        {
            facts["priorDesignPacket"] = prior;
        }

        return await station.DesignIntake().Run(LineStation.Context("step.design.intake", LineStation.DesignAddress, facts), TestContext.Current.CancellationToken);
    }

    private const string Revision = """{"decisions":[{"id":"D1","pick":"D1.b"}],"notes":[]}""";

    private static async Task<string> PriorPacket(LineStation station) => LineStation.Fact(await station.DesignEvidenceRun(), "designPacket");

    [Fact]
    public async Task A_first_pass_is_fresh_and_writes_the_eight_seeds_with_the_loaded_scope()
    {
        var result = await Run(new LineStation());

        Assert.Equal("fresh", result.Result!.Outcome.Value);
        Assert.Equal(_seeds, ScopingStepContexts.Keys(result));
        var scope = JsonNode.Parse(LineStation.Fact(result, "scope"))!;
        Assert.Equal(LineStation.BaseSha, (string?)scope["baseSha"]);
        Assert.Equal("expiry", (string?)scope["change"]);
        Assert.Equal("S1", (string?)scope["scopeMap"]!["surfaces"]![0]!["id"]);
        Assert.Equal("AC-1", (string?)scope["acceptance"]!["items"]![0]!["id"]);
        Assert.Equal("{}", LineStation.Fact(result, "design"));
    }

    [Fact]
    public async Task An_open_question_is_one_no_answer_batch_answered_and_the_answer_log_is_loaded()
    {
        var result = await Run(new LineStation());

        var scope = JsonNode.Parse(LineStation.Fact(result, "scope"))!;
        Assert.Equal(["Q2"], scope["openQuestions"]!.AsArray().Select(static q => (string?)q!["id"]), StringComparer.Ordinal);
        Assert.Equal("A1", (string?)scope["answerLog"]![0]!["id"]);
        Assert.StartsWith("sha256:", (string?)scope["acceptanceHash"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_revision_from_stop_two_is_revise_and_seeds_the_answers_with_it()
    {
        var station = new LineStation();

        var result = await Run(station, revision: Revision, prior: await PriorPacket(station));

        Assert.Equal("revise", result.Result!.Outcome.Value);
        Assert.Equal("D1", (string?)JsonNode.Parse(LineStation.Fact(result, "answers"))![0]!["decisions"]![0]!["id"]);
        Assert.Equal("[]", LineStation.Fact(result, "accepted"));
    }

    [Fact]
    public async Task A_revision_with_only_a_default_note_is_still_revise()
    {
        const string revision = """{"decisions":[],"notes":["owner asked for a revision, no text"]}""";

        var station = new LineStation();

        var result = await Run(station, revision: revision, prior: await PriorPacket(station));

        Assert.Equal("revise", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_scope_packet_that_hashes_to_something_else_fails_the_Step_naming_both_hashes()
    {
        var other = LineStation.ScopePacket.Replace("sha256:", "sha256:00", StringComparison.Ordinal);

        var result = await Run(new LineStation(), other);

        Assert.Null(result.Result);
        Assert.Contains("SND-LINE-001", result.Failure, StringComparison.Ordinal);
        Assert.Contains("hashes to " + ScopeHash(), result.Failure, StringComparison.Ordinal);
        Assert.Contains("sha256:00", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scope_packet_fact_with_no_directory_fails_the_Step_with_the_fix()
    {
        var result = await Run(new LineStation(), $$"""{"hash":"{{ScopeHash()}}"}""");

        Assert.Null(result.Result);
        Assert.Contains("names no packet directory", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scope_packet_missing_a_file_fails_the_Step()
    {
        var station = new LineStation();
        var files = new InMemoryPacketFiles().Put(LineStation.ScopeDirectory, "scope-map.json", "{}");

        var result = await new DesignIntakeStep(station.Packets, files).Run(
            LineStation.Context("step.design.intake", LineStation.DesignAddress, new ScopingFacts { ["scopePacket"] = LineStation.ScopePacket, ["designRevision"] = "x" }),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("acceptance.json", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_packet_aw_packets_cannot_read_fails_the_Step_rather_than_passing_it()
    {
        var station = new LineStation();
        _ = station.Packets.Unreadable(LineStation.ScopeDirectory);

        var result = await Run(station);

        Assert.Null(result.Result);
        Assert.Contains("not a readable packet", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_revision_with_a_prior_packet_loads_that_design_not_an_empty_one()
    {
        var station = new LineStation();
        var prior = await PriorPacket(station);

        var result = await Run(station, revision: Revision, prior: prior);

        Assert.Equal("revise", result.Result!.Outcome.Value);
        var design = JsonNode.Parse(LineStation.Fact(result, "design"))!;
        Assert.Equal("# Design\n\n## Summary\nB holds.\n\n## Detail\nMore.\n", (string?)design["spec"]);
        Assert.Equal(["sec-1", "sec-2"], design["sections"]!.AsArray().Select(static s => (string?)s!["id"]), StringComparer.Ordinal);
        Assert.Equal("AC-1", (string?)design["acceptance"]!["items"]![0]!["id"]);
        Assert.StartsWith("sha256:", (string?)design["acceptanceHash"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_revision_with_no_prior_packet_fails_the_Step_with_the_fix_and_never_seeds_an_empty_design()
    {
        var result = await Run(new LineStation(), revision: Revision);

        Assert.Null(result.Result);
        Assert.Contains("priorDesignPacket", result.Failure, StringComparison.Ordinal);
        Assert.Contains("fix:", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_revision_whose_prior_packet_hash_moved_fails_with_SND_LINE_001()
    {
        var station = new LineStation();
        var prior = (await PriorPacket(station)).Replace("sha256:", "sha256:00", StringComparison.Ordinal);

        var result = await Run(station, revision: Revision, prior: prior);

        Assert.Null(result.Result);
        Assert.Contains("SND-LINE-001", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_first_pass_ignores_a_prior_packet_text_and_stays_fresh_with_an_empty_design()
    {
        var result = await Run(new LineStation(), prior: "First pass: no previous design.");

        Assert.Equal("fresh", result.Result!.Outcome.Value);
        Assert.Equal("{}", LineStation.Fact(result, "design"));
    }

    private static string ScopeHash() => InMemoryPackets.HashOf(new PacketRef(PacketKind.Scoping, LineStation.ScopeDirectory));
}
