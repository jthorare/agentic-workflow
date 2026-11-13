using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Ops;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Features.ListTriggerEvents;
using Aw.Sindri.Features.ListWorkflowTemplates;
using Aw.Sindri.Features.PinWorkflowOutput;
using Aw.Sindri.Features.TestWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.Graph.GraphFixtures;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.WorkflowsPageP0;

/// <summary>The Workflows page P0 contract (#3191, spec 2026-11-12 §9 and §14): the shapes the ui and engine lanes compile against. Behaviour comes with later phases; these pin the names and the stub refusal.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsPageContractTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@3:12");

    [Fact]
    public void The_run_payloads_carry_a_mode()
    {
        var finished = JsonSerializer.Serialize(new WorkflowRunFinishedPayload(_run, "succeeded", Error: null, "engine", WorkflowRunMode.Test), _json);
        var started = JsonSerializer.Serialize(new WorkflowRunStartedPayload(_run, WorkflowId.From("WF-00000000000000000000000000000001"), 3, "{}", "owner", WorkflowRunMode.Live), _json);
        var cancelled = JsonSerializer.Serialize(new WorkflowRunCancelledPayload(_run, Reason: null, "owner", WorkflowRunMode.Test), _json);

        Assert.Contains("\"mode\":\"test\"", finished, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"live\"", started, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"test\"", cancelled, StringComparison.Ordinal);
    }

    [Fact]
    public void The_public_run_form_has_status_and_mode_and_an_old_event_reads_as_live()
    {
        var finished = JsonSerializer.Deserialize<WorkflowRunPublic>("{\"runId\":\"nightly@3:12\",\"status\":\"failed\",\"error\":\"x\",\"by\":\"engine\",\"mode\":\"test\"}", _json)!;
        var old = JsonSerializer.Deserialize<WorkflowRunPublic>("{\"runId\":\"nightly@3:12\",\"reason\":\"stopped\",\"by\":\"owner\"}", _json)!;

        Assert.Equal(("failed", WorkflowRunMode.Test), (finished.Status, finished.Mode));
        Assert.Null(old.Status);
        Assert.Equal(WorkflowRunMode.Live, old.Mode);
    }

    [Fact]
    public void A_tool_is_not_read_only_until_something_marks_it()
    {
        Assert.False(new ConnectionTool("search", "Looks things up.", "mcp").ReadOnly);
        Assert.True(new ConnectionTool("search", "Looks things up.", "mcp", ReadOnly: true).ReadOnly);
        Assert.False(new ToolkitTool("t", "d", "{}", "{}").ReadOnly);
    }

    [Fact]
    public void Replace_node_parses_as_an_op()
    {
        var parsed = GraphOpJson.Parse("[{\"op\":\"replace_node\",\"node\":\"daily\",\"type\":\"manual\",\"title\":\"By hand\",\"config\":{}}]");

        Assert.True(parsed.IsSuccess);
        var op = Assert.IsType<ReplaceNodeOp>(Assert.Single(parsed.Value));
        Assert.Equal(("daily", "manual", "By hand"), (op.Node.Value, op.Type, op.Title));
        Assert.Contains("\"op\":\"replace_node\"", GraphOpJson.Write(parsed.Value), StringComparison.Ordinal);
    }

    [Fact]
    public void Replace_node_on_a_node_that_is_not_the_trigger_is_refused_with_SND_GRAPH_112()
    {
        var spec = Spec([Trigger("daily"), Search("a")], [Edge("daily", "a")]);

        var result = GraphOps.Apply(spec, 1, 1, [new ReplaceNodeOp(NodeId.From("a"), "schedule")], Validator());

        Assert.True(result.IsError);
        Assert.Equal("SND-GRAPH-112", result.Error.Code);
    }

    [Fact]
    public void A_node_run_has_a_raw_text_that_is_null_until_the_engine_keeps_it()
    {
        var row = new NodeRunView(NodeId.From("ask"), 0, "succeeded", Input: null, Output: null, Truncated: false, Error: null);

        Assert.Null(row.Raw);
        Assert.Equal("the model said", (row with { Raw = "the model said" }).Raw);
    }

    [Fact]
    public async Task A_finding_carries_a_stable_id_its_node_a_cause_and_a_fix()
    {
        using var rig = new WorkflowWriteRig();
        var service = new ValidateWorkflowService(rig.Workflows, rig.Checker);
        const string noTrigger = "{\"version\":1,\"name\":\"No trigger\",\"nodes\":[{\"id\":\"tell\",\"type\":\"notify\",\"config\":{\"title\":\"x\"}}],\"edges\":[]}";

        var result = await service.Handle(new ValidateWorkflowRequest(Slug: null, Revision: null, noTrigger), Token);

        var finding = result.Value.Findings.First(static f => f.Severity == "error");
        Assert.False(string.IsNullOrWhiteSpace(finding.Id));
        Assert.Equal(finding.Message, finding.Cause);
        Assert.Equal(finding.Nodes.Count > 0 ? finding.Nodes[0] : null, finding.Node);
        Assert.False(string.IsNullOrWhiteSpace(finding.Fix));
        Assert.Equal(finding.Id, result.Value.Findings.First(f => f.Code == finding.Code && f.Node == finding.Node && f.Field == finding.Field).Id);
    }

    [Fact]
    public async Task The_test_route_stub_refuses_with_not_built()
    {
        var result = await new TestWorkflowService().Handle(new TestWorkflowRequest(Slug, Revision: null, Trigger: null, TriggerEvent: null, Pins: null, WithModel: false, WithAgents: false, "owner"), Token);

        Assert.Equal("SND-GRAPH-119", result.Error.Code);
    }

    [Fact]
    public async Task The_pin_route_stub_refuses_with_not_built()
    {
        var result = await new PinWorkflowOutputService().Handle(new PinWorkflowOutputRequest(Slug, Revision: null, "fetch", Run: null, Output: "{}", "owner"), Token);

        Assert.Equal("SND-GRAPH-119", result.Error.Code);
    }

    [Fact]
    public async Task The_trigger_events_route_stub_refuses_with_not_built()
    {
        var result = await new ListTriggerEventsService().Handle(new ListTriggerEventsRequest("github.pr.opened", Connection: null), Token);

        Assert.Equal("SND-GRAPH-119", result.Error.Code);
    }

    [Fact]
    public void The_new_response_shapes_serialise_in_camel_case()
    {
        var events = JsonSerializer.Serialize(new ListTriggerEventsResponse("github.pr.opened", [new TriggerEventView("e1", DateTimeOffset.UnixEpoch, "PR #1 opened", JsonNode.Parse("{}"), WouldMatch: true)], Example: null), _json);
        var templates = JsonSerializer.Serialize(new ListWorkflowTemplatesResponse([new WorkflowTemplateView("A", "Review new PRs", "github.pr.opened", 3, ["connection:github"], LastRunAt: null)]), _json);

        Assert.Contains("\"wouldMatch\":true", events, StringComparison.Ordinal);
        Assert.Contains("\"lastRunAt\":null", templates, StringComparison.Ordinal);
    }
}
