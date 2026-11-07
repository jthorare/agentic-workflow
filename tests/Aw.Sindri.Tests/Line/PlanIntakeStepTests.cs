using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.plan.intake' on the block runtime (line spec §6.1): verifies the scope packet and the candidate design packet and writes the seven seeds, the design among them.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanIntakeStepTests
{
    private static async Task<Core.Workflow.Blocks.BlockStepResult> Run(LineStation station, string designPacket, string? scopePacket = null) =>
        await station.PlanIntake().Run(
            LineStation.Context("step.plan.intake", LineStation.PlanAddress, new ScopingFacts { ["scopePacket"] = scopePacket ?? LineStation.ScopePacket, ["designPacket"] = designPacket }),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_candidate_design_is_loaded_from_its_packet_and_the_seven_seeds_are_written()
    {
        var result = await new LineStation().PlanIntakeRun();

        Assert.Equal("loaded", result.Result!.Outcome.Value);
        Assert.Equal(["anchors", "answerLog", "answers", "design", "findings", "holdoutSets", "plan", "questions"], ScopingStepContexts.Keys(result));
        var design = JsonNode.Parse(LineStation.Fact(result, "design"))!;
        Assert.Equal("sec-1", (string?)design["sections"]![0]!["id"]);
        Assert.StartsWith("sha256:", (string?)design["packet"]!["hash"], StringComparison.Ordinal);
        Assert.StartsWith("sha256:", (string?)design["acceptanceHash"], StringComparison.Ordinal);
        Assert.Equal("{}", LineStation.Fact(result, "plan"));
        Assert.Equal("[]", LineStation.Fact(result, "holdoutSets"));
    }

    [Fact]
    public async Task The_anchors_are_the_scope_map_keys_of_the_surfaces_the_design_covers()
    {
        var result = await new LineStation().PlanIntakeRun();

        Assert.Equal("""["code:src/a.ts"]""", LineStation.Fact(result, "anchors"));
    }

    [Fact]
    public async Task A_design_packet_that_hashes_to_something_else_fails_the_Step_naming_both_hashes()
    {
        var station = new LineStation();
        var evidence = await station.DesignEvidenceRun();
        var packet = JsonNode.Parse(LineStation.Fact(evidence, "designPacket"))!;
        var moved = "sha256:" + new string('0', 64);
        packet["hash"] = moved;

        var result = await Run(station, packet.ToJsonString());

        Assert.Null(result.Result);
        Assert.Contains("SND-LINE-001", result.Failure, StringComparison.Ordinal);
        Assert.Contains(moved, result.Failure, StringComparison.Ordinal);
        Assert.Contains("design packet", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_design_packet_built_on_another_scope_packet_fails_the_Step()
    {
        var station = new LineStation();
        var evidence = await station.DesignEvidenceRun();
        _ = await station.Area.Write(LineStation.DesignAddress, "design-packet/index.json", """{"schemaVersion":1,"kind":"design","parents":["sha256:other"],"acceptance":{"self":null,"extends":null}}""", append: false, TestContext.Current.CancellationToken);

        var result = await Run(station, LineStation.Fact(evidence, "designPacket"));

        Assert.Null(result.Result);
        Assert.Contains("does not name the scope packet", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scope_packet_that_hashes_to_something_else_fails_the_Step()
    {
        var station = new LineStation();
        var evidence = await station.DesignEvidenceRun();

        var result = await Run(station, LineStation.Fact(evidence, "designPacket"), LineStation.ScopePacket.Replace("sha256:", "sha256:00", StringComparison.Ordinal));

        Assert.Null(result.Result);
        Assert.Contains("SND-LINE-001", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_design_packet_fact_with_only_a_hash_fails_the_Step_with_the_fix()
    {
        var result = await Run(new LineStation(), "\"sha256:abc\"");

        Assert.Null(result.Result);
        Assert.Contains("names no packet directory", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_design_packet_with_a_file_gone_fails_the_Step()
    {
        var station = new LineStation();
        var evidence = await station.DesignEvidenceRun();
        var packet = JsonNode.Parse(LineStation.Fact(evidence, "designPacket"))!;
        var files = new InMemoryPacketFiles().Put(LineStation.ScopeDirectory, "scope-map.json", "{}");

        var result = await new PlanIntakeStep(station.Packets, files).Run(
            LineStation.Context("step.plan.intake", LineStation.PlanAddress, new ScopingFacts { ["scopePacket"] = LineStation.ScopePacket, ["designPacket"] = packet.ToJsonString() }),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("acceptance.json", result.Failure, StringComparison.Ordinal);
        _ = PacketKind.Design;
    }
}
