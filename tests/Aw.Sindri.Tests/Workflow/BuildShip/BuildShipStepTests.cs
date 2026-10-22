using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>The build and ship stations' Steps that read the worktree through IRunDiff, and the ship return, on the block runtime (line spec §7.1, §7.2, §7.5).</summary>
[Lane(TestLane.Unit)]
public sealed class BuildShipStepTests
{
    private const string Tree = "/work/p1";

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private static Dictionary<string, string> PrepareFacts() => new(StringComparer.Ordinal)
    {
        ["phasePacket"] = """{"hash":"sha256:aa","dir":"packets/p1"}""",
        ["workspace"] = $"\"{Tree}\"",
        ["buildNotes"] = "{}",
        ["lineContext"] = "{}",
    };

    [Fact]
    public async Task Prepare_on_a_clean_worktree_seeds_the_build_facts_with_the_phase_packet_in_force()
    {
        var diff = new FakeRunDiff();
        diff.Add(Tree);

        var result = await new BuildPrepareStep(diff).Run(BuildShipContexts.Of("step.build.prepare", PrepareFacts()), TestContext.Current.CancellationToken);

        Assert.Equal("ready", result.Result!.Outcome.Value);
        Assert.Equal("sha256:aa", (string?)Out(result, "activePacket")["hash"]);
        Assert.Equal(Tree, (string?)Out(result, "activePacket")["repo"]);   // the checkout step.build.amend gives aw-packets
        _ = Assert.IsType<JsonObject>(Out(result, "answers"));   // the scope desk writes an object ({ allow: [...] }); amend reads it
        Assert.Equal(["activePacket", "answers", "built", "change", "findings", "redTests"], result.Result.Outputs.Keys.Select(static k => k.Value[5..]).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Prepare_on_a_dirty_worktree_fails_the_Step_and_names_the_path()
    {
        var diff = new FakeRunDiff();
        diff.Add(Tree);
        diff.Touch(Tree);

        var result = await new BuildPrepareStep(diff).Run(BuildShipContexts.Of("step.build.prepare", PrepareFacts()), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains(Tree, result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prepare_without_a_worktree_fails_the_Step()
    {
        var result = await new BuildPrepareStep(new FakeRunDiff()).Run(BuildShipContexts.Of("step.build.prepare", PrepareFacts()), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task Prepare_without_the_phase_packet_fails_the_Step()
    {
        var facts = PrepareFacts();
        _ = facts.Remove("phasePacket");

        var result = await new BuildPrepareStep(new FakeRunDiff()).Run(BuildShipContexts.Of("step.build.prepare", facts), TestContext.Current.CancellationToken);

        Assert.Contains("phasePacket", result.Failure, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> DriftFacts(string provenHead) => new(StringComparer.Ordinal)
    {
        ["built"] = $$"""{"head":"{{provenHead}}","commits":[]}""",
        ["change"] = $$"""{"repo":"r","branch":"b","base":"main","head":"{{provenHead}}"}""",
        ["workspace"] = $"\"{Tree}\"",
    };

    [Fact]
    public async Task Drift_is_same_when_review_left_the_head_alone()
    {
        var diff = new FakeRunDiff();
        diff.Add(Tree, "abc");

        var result = await new PhaseDriftStep(diff).Run(BuildShipContexts.Of("step.phase.drift", DriftFacts("abc")), TestContext.Current.CancellationToken);

        Assert.Equal("same", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
    }

    [Fact]
    public async Task Drift_is_moved_with_the_new_head_in_built_and_change_when_review_committed()
    {
        var diff = new FakeRunDiff();
        diff.Add(Tree, "abc");
        diff.Advance(Tree);

        var result = await new PhaseDriftStep(diff).Run(BuildShipContexts.Of("step.phase.drift", DriftFacts("abc")), TestContext.Current.CancellationToken);

        Assert.Equal("moved", result.Result!.Outcome.Value);
        Assert.Equal("abc+", (string?)Out(result, "built")["head"]);
        Assert.Equal("abc+", (string?)Out(result, "change")["head"]);
        Assert.Equal("main", (string?)Out(result, "change")["base"]);
    }

    [Fact]
    public async Task Drift_with_an_unreadable_worktree_fails_the_Step()
    {
        var result = await new PhaseDriftStep(new FakeRunDiff()).Run(BuildShipContexts.Of("step.phase.drift", DriftFacts("abc")), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains(Tree, result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Return_turns_the_findings_into_the_notes_the_builder_reads()
    {
        var result = await new ShipReturnStep().Run(BuildShipContexts.Of("step.ship.return", new Dictionary<string, string>(StringComparer.Ordinal) { ["findings"] = """[{"rule":"SHIP-SHA-001"}]""" }), TestContext.Current.CancellationToken);

        Assert.Equal("noted", result.Result!.Outcome.Value);
        Assert.Equal("SHIP-SHA-001", (string?)Out(result, "returnNotes")["findings"]![0]!["rule"]);
    }

    [Fact]
    public async Task Return_without_findings_fails_the_Step()
    {
        var result = await new ShipReturnStep().Run(BuildShipContexts.Of("step.ship.return", []), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
    }
}
