using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>'step.ship.freeze' on the block runtime (line spec §7.5, SHIP-SHA-001): proof, review, hand-off and the branch agree on one head, then the hand-off is machine-frozen.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipFreezeStepTests
{
    private const string Tree = "/work/p1";
    private const string Dir = "/run/handoff";

    private readonly InMemoryPackets _packets = new();
    private readonly FakeRunDiff _diff = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static StepContext Context(string draftHead = "h1", string proofHead = "h1", string reviewHead = "h1") => BuildShipContexts.Of("step.ship.freeze", new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["handoffDraft"] = $$"""{"dir":"{{Dir}}","head":"{{draftHead}}","workspace":"{{Tree}}"}""",
        ["proofPack"] = $$"""{"head":"{{proofHead}}"}""",
        ["review"] = $$"""{"head":"{{reviewHead}}"}""",
    });

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private ShipFreezeStep Step(string branchHead = "h1")
    {
        _diff.Add(Tree, branchHead);
        return new ShipFreezeStep(_diff, _packets);
    }

    [Fact]
    public async Task Four_matching_heads_machine_freeze_the_handoff_and_report_it()
    {
        var result = await Step().Run(Context(), Token);

        Assert.Equal("frozen", result.Result!.Outcome.Value);
        var (verb, packet, by) = Assert.Single(_packets.Calls);
        Assert.Equal(("freeze", PacketKind.Handoff, Dir, Tree, PacketFreezer.Machine), (verb, packet.Kind, packet.Directory, packet.Repo, by));
        Assert.Equal(Dir, (string?)Out(result, "handoff")["dir"]);
        Assert.Equal(InMemoryPackets.HashOf(packet), (string?)Out(result, "handoff")["hash"]);
    }

    [Theory]
    [InlineData("h2", "h1", "h1", "h1")]
    [InlineData("h1", "h2", "h1", "h1")]
    [InlineData("h1", "h1", "h2", "h1")]
    [InlineData("h1", "h1", "h1", "h2")]
    public async Task Any_head_that_differs_is_stale_and_names_the_rule_and_freezes_nothing(string draft, string proof, string review, string branch)
    {
        var result = await Step(branch).Run(Context(draft, proof, review), Token);

        Assert.Equal("stale", result.Result!.Outcome.Value);
        var finding = Out(result, "findings").AsArray().Single()!;
        Assert.Equal("SHIP-SHA-001", (string?)finding["rule"]);
        Assert.Contains("h2", (string?)finding["message"], StringComparison.Ordinal);
        Assert.Empty(_packets.Calls);
    }

    [Fact]
    public async Task A_handoff_that_fails_its_checks_fails_the_Step_and_names_the_rule()
    {
        _ = _packets.Fails(Dir, new PacketFailure("PKT-HND-007", "packet", "pr.json is wrong", Fix: null));

        var result = await Step().Run(Context(), Token);

        Assert.Null(result.Result);
        Assert.Contains("PKT-HND-007", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_head_or_worktree_fails_the_Step()
    {
        var noHead = BuildShipContexts.Of("step.ship.freeze", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["handoffDraft"] = $$"""{"dir":"{{Dir}}","workspace":"{{Tree}}"}""",
            ["proofPack"] = """{"head":"h1"}""",
            ["review"] = """{"head":"h1"}""",
        });

        var missingHead = await Step().Run(noHead, Token);
        var missingTree = await new ShipFreezeStep(new FakeRunDiff(), _packets).Run(Context(), Token);

        Assert.Null(missingHead.Result);
        Assert.Null(missingTree.Result);
    }
}
