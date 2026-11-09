using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The app's workflow records read every field of the workflow goldens Sindri serves (#2258 PR 8a, PR 17 part 1), field for field.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowGoldenContractTests
{
    private static T Read<T>(string slug, string scenario)
        where T : class
    {
        var read = AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        return read.Value;
    }

    [Fact]
    public void The_app_DTOs_read_every_workflow_golden_Sindri_serves()
    {
        var list = Read<WorkflowList>("workflows", "list-two");
        Assert.Equal(["morning-check", "weekly-report"], list.Workflows.Select(w => w.Slug), StringComparer.Ordinal);
        Assert.Equal((1, 1, "owner"), (list.Workflows[0].ActiveRevision, list.Workflows[0].LatestRevision, list.Workflows[0].ChangedBy));
        Assert.Null(list.Workflows[1].ActiveRevision);
        Assert.Empty(Read<WorkflowList>("workflows", "list-empty").Workflows);

        var one = Read<WorkflowView>("workflows-slug", "found");
        Assert.Equal(("WF-00000000000000000000000000000001", "morning-check", "Morning check", 1, 1, 2), (one.Id, one.Slug, one.Name, one.Revision, one.ActiveRevision, one.LatestRevision));
        Assert.Equal(("owner", "session:golden"), (one.CreatedBy, one.ChangedBy));
        Assert.Null(one.ArchivedAt);
        Assert.Equal(2, one.Spec.GetProperty("nodes").GetArrayLength());
        NodePositionView[] positions = [new("start", 0, 0), new("agent", 280, 0)];
        Assert.Equal(positions, one.Positions);

        var runs = Read<WorkflowRunList>("workflows-slug-runs", "list-three");
        Assert.Equal(("morning-check", 3), (runs.Slug, runs.Runs.Count));
        Assert.Equal(("morning-check@1:2", 1, "failed", "schedule", "node 'agent' failed"), (runs.Runs[1].Id, runs.Runs[1].Revision, runs.Runs[1].Status, runs.Runs[1].Trigger, runs.Runs[1].Error));
        Assert.Null(runs.Runs[0].EndedAt);
        Assert.Empty(Read<WorkflowRunList>("workflows-slug-runs", "list-empty").Runs);

        var run = Read<WorkflowRunView>("workflow-runs-run", "finished");
        Assert.Equal(("morning-check@1:1", "morning-check", 1, "succeeded"), (run.Id, run.Slug, run.Revision, run.Status));
        Assert.Equal("manual", run.Trigger!.Value.GetProperty("kind").GetString());
        Assert.Equal("no drift", run.Outputs!.Value.GetProperty("summary").GetString());
        Assert.True(run.Error is null && run.ItemId is null);
        Assert.Equal(["agent", "start"], run.Nodes.Select(n => n.Node), StringComparer.Ordinal);
        Assert.Equal((0, "succeeded", false), (run.Nodes[0].ItemIndex, run.Nodes[0].Status, run.Nodes[0].Truncated));
        Assert.Null(run.Nodes[0].Error);
        Assert.Equal("Look for drift", run.Nodes[0].Input!.Value.GetProperty("prompt").GetString());
        Assert.Equal("no drift", run.Nodes[0].Output!.Value.GetProperty("text").GetString());
        Assert.Null(run.Nodes[1].Input);
    }

    [Fact]
    public void A_running_run_has_no_outputs_and_a_node_without_output_yet()
    {
        var run = Read<WorkflowRunView>("workflow-runs-run", "running");

        Assert.True(run.Status == "running" && run.Outputs is null && run.EndedAt is null);
        Assert.Null(run.Nodes[0].Output);
        Assert.Equal(JsonValueKind.Object, run.Nodes[0].Input!.Value.ValueKind);
    }

    [Fact]
    public void The_routes_escape_a_run_ids_at_sign_and_colon()
    {
        Assert.Equal(("/sindri/workflows", "/sindri/workflows/morning-check", "/sindri/workflows/morning-check/runs", "/sindri/workflow-runs/morning-check%401%3A1"),
            (WorkflowRoutes.List(), WorkflowRoutes.One("morning-check"), WorkflowRoutes.Runs("morning-check"), WorkflowRoutes.Run("morning-check@1:1")));
    }

    [Fact]
    public void The_write_routes_escape_the_slug()
    {
        Assert.Equal(("/sindri/workflows/morning-check/ops", "/sindri/workflows/morning-check/activate", "/sindri/workflows/morning-check/pause", "/sindri/workflows/blocks"),
            (WorkflowRoutes.Ops("morning-check"), WorkflowRoutes.Activate("morning-check"), WorkflowRoutes.Pause("morning-check"), WorkflowRoutes.Blocks()));
    }

    [Fact]
    public void The_app_DTOs_read_the_pause_and_archive_goldens()
    {
        var paused = Read<WorkflowPaused>("workflows-slug-pause", "paused");
        Assert.Equal(("morning-check", 1), (paused.Slug, paused.LatestRevision));

        var archived = Read<WorkflowArchived>("workflows-slug", "archived");
        Assert.Equal(("morning-check", DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)), (archived.Slug, archived.ArchivedAt));
    }

    [Fact]
    public void The_write_bodies_send_every_field_even_when_null()
    {
        var activate = Encoding.UTF8.GetString(AwJson.Serialize(new ActivateWorkflowBody(Revision: null), SindriApiClient.Json));
        var ops = Encoding.UTF8.GetString(AwJson.Serialize(new ApplyWorkflowOpsBody(2, JsonDocument.Parse("[{\"op\":\"remove_node\",\"node\":\"a\"}]").RootElement), SindriApiClient.Json));

        Assert.Equal("{\"revision\":null}", activate);
        Assert.Equal("{\"baseRevision\":2,\"ops\":[{\"op\":\"remove_node\",\"node\":\"a\"}]}", ops);
    }

    [Fact]
    public void The_block_list_reads_groups_with_titles_and_config_fields()
    {
        const string Served = """{"groups":[{"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null},{"type":"run_agent","title":"Run an agent","isTrigger":false,"config":[{"name":"prompt","type":"text","required":true}],"outputs":{"text":"text"},"branches":["out"],"exampleConfig":{"prompt":"hi"},"exampleSpec":null}]}]}""";

        var read = AwJson.Deserialize<WorkflowBlockList>(Encoding.UTF8.GetBytes(Served), SindriApiClient.Json);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        var group = Assert.Single(read.Value.Groups);
        Assert.Equal(("Triggers", "Run an agent", true), (group.Group, group.Blocks[1].Title, group.Blocks[0].IsTrigger));
        Assert.Equal(new WorkflowBlockField("prompt", "text", Required: true), group.Blocks[1].Config[0]);
        Assert.Equal("hi", group.Blocks[1].ExampleConfig!.Value.GetProperty("prompt").GetString());
        Assert.Null(group.Blocks[0].ExampleConfig);
    }
}
