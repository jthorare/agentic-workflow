using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The 'workflow_result' block (agent-first PR 9, #2258): it reads a finished run through its port and starts nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowResultBlockTests
{
    private static readonly WorkflowRunId _found = WorkflowRunId.From("triage@1:7");

    private static BlockContext Caller(JsonObject config) =>
        new(WorkflowRunId.From("nightly@2:5"), NodeId.From("result"), config, (JsonObject)config.DeepClone(), static _ => null);

    private static Task<Result<BlockResult>> Run(IGraphResultPort port, JsonObject config) =>
        new WorkflowResultBlock(port).Execute(Caller(config), TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task A_node_with_no_run_reads_the_latest_succeeded_run_and_hands_on_its_result()
    {
        var port = new ScriptedResultPort(new GraphRunResult(_found, "succeeded", DateTimeOffset.Parse("2026-11-09T02:00:00Z", System.Globalization.CultureInfo.InvariantCulture), new JsonObject { ["fetch"] = new JsonObject { ["count"] = 3 } }));

        var result = await Run(port, new JsonObject { ["workflow"] = "triage" });

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var asked = Assert.Single(port.Asked);
        Assert.Equal(("triage", null), (asked.Workflow.Value, asked.Run));
        Assert.Equal(("triage@1:7", "succeeded"), (result.Value.Outputs["runId"]!.GetValue<string>(), result.Value.Outputs["status"]!.GetValue<string>()));
        Assert.Equal("2026-11-09T02:00:00.0000000+00:00", result.Value.Outputs["finishedAt"]!.GetValue<string>());
        Assert.Equal(3, result.Value.Outputs["outputs"]!["fetch"]!["count"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("latest-succeeded", null)]
    [InlineData("triage@1:7", "triage@1:7")]
    public async Task The_run_setting_picks_the_latest_succeeded_run_or_one_run_by_id(string run, string? asked)
    {
        var port = new ScriptedResultPort(new GraphRunResult(_found, "succeeded", FinishedAt: null, Outputs: null));

        _ = await Run(port, new JsonObject { ["workflow"] = "triage", ["run"] = run });

        Assert.Equal(asked, Assert.Single(port.Asked).Run?.Value);
    }

    [Theory]
    [InlineData("Not A Slug", "latest-succeeded")]
    [InlineData("triage", "not-a-run")]
    public async Task A_bad_workflow_or_run_value_fails_the_node_and_asks_the_port_nothing(string workflow, string run)
    {
        var port = new ScriptedResultPort(new GraphRunResult(_found, "succeeded", FinishedAt: null, Outputs: null));

        var result = await Run(port, new JsonObject { ["workflow"] = workflow, ["run"] = run });

        Assert.Equal(SindriErrors.GraphBlockFailed.Code, result.Error.Code);
        Assert.Empty(port.Asked);
    }

    [Fact]
    public async Task A_refusal_of_the_port_is_the_nodes_failure()
    {
        var port = new ScriptedResultPort(SindriErrors.GraphResultUnavailable.With("'triage' has no succeeded run"));

        var result = await Run(port, new JsonObject { ["workflow"] = "triage" });

        Assert.Equal(SindriErrors.GraphResultUnavailable.Code, result.Error.Code);
    }

    private sealed class ScriptedResultPort(Result<GraphRunResult> answer) : IGraphResultPort
    {

        public List<(WorkflowSlug Workflow, WorkflowRunId? Run)> Asked { get; } = [];

        public ValueTask<Result<GraphRunResult>> Read(WorkflowSlug workflow, WorkflowRunId? run, CancellationToken cancellationToken)
        {
            Asked.Add((workflow, run));
            return ValueTask.FromResult(answer);
        }
    }
}
