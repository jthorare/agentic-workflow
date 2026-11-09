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
        Assert.Equal(new[] { new NodePositionView("start", 0, 0), new NodePositionView("agent", 280, 0) }, one.Positions);

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
}
