using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The flow blocks as the validator sees them: their declared outputs are what later nodes may read, and an 'any' output is never a type mismatch.</summary>
[Lane(TestLane.Unit)]
public sealed class FlowBlockValidationTests
{
    private static GraphValidator Validator() => GraphFixtures.Validator(blocks: GraphBlockRegistry.Standard(
        new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more:
        GraphBlockRegistry.FlowBlocks(new FlowFakes.MemorySeenStore(), new FlowFakes.RecordingItemPort(), new FlowFakes.ScriptedCallPort(), new FlowFakes.ScriptedHttpPort())).Types);

    private static GraphNode Node(string id, string type, JsonObject config) => new(NodeId.From(id), type, Title: null, config);

    private static GraphSpec Spec(params GraphNode[] nodes) =>
        GraphFixtures.Spec(nodes, [.. nodes.Zip(nodes.Skip(1), static (a, b) => GraphFixtures.Edge(a.Id.Value, b.Id.Value))]);

    [Fact]
    public void A_callee_reads_its_payload_and_a_later_node_reads_what_the_flow_blocks_declare()
    {
        var spec = Spec(
            Node("start", "workflow_call", []),
            Node("fresh", "only_new", new JsonObject { ["items"] = "{{trigger.payload.prs}}", ["key"] = "number" }),
            Node("ask", "http", new JsonObject { ["url"] = "https://api.example.com/x", ["body"] = new JsonObject { ["by"] = "{{trigger.parentRun}}" } }),
            Node("make", "create_item", new JsonObject { ["title"] = "Review {{fresh.count}} PRs ({{ask.status}})", ["body"] = "{{ask.text}}" }),
            Node("next", "run_workflow", new JsonObject { ["workflow"] = "triage", ["payload"] = new JsonObject { ["item"] = "{{make.itemId}}" } }));

        var result = Validator().Validate(spec, GraphMode.Activate);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void A_reference_to_something_the_trigger_does_not_hand_on_is_refused()
    {
        var spec = Spec(Node("start", "workflow_call", []), Node("tell", "notify", new JsonObject { ["title"] = "{{trigger.firedAt}}" }));

        var result = Validator().Validate(spec, GraphMode.Activate);

        Assert.Contains(result.Findings, static f => f.Code == "GRAPH-REF-002");
    }

    [Fact]
    public void An_output_declared_any_fits_a_number_field_and_a_string_output_does_not()
    {
        var fits = Spec(
            Node("start", "workflow_call", []), Node("ask", "http", new JsonObject { ["url"] = "https://api.example.com/x" }),
            Node("next", "run_workflow", new JsonObject { ["workflow"] = "triage", ["wait"] = true, ["waitSeconds"] = "{{ask.body}}" }));
        var clash = Spec(
            Node("start", "workflow_call", []), Node("ask", "http", new JsonObject { ["url"] = "https://api.example.com/x" }),
            Node("next", "run_workflow", new JsonObject { ["workflow"] = "triage", ["wait"] = true, ["waitSeconds"] = "{{ask.text}}" }));

        Assert.DoesNotContain(Validator().Validate(fits, GraphMode.Activate).Findings, static f => f.Code == "GRAPH-REF-005");
        Assert.Contains(Validator().Validate(clash, GraphMode.Activate).Findings, static f => f.Code == "GRAPH-REF-005");
    }
}
