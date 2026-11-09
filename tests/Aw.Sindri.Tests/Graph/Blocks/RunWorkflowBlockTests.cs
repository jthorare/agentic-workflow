using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class RunWorkflowBlockTests
{
    private static BlockContext Caller(JsonObject config, int? depth = null, string? parentRun = null, string run = "nightly@2:5")
    {
        var values = new Dictionary<string, System.Text.Json.Nodes.JsonNode?>(StringComparer.Ordinal);
        if (depth is { } d)
        {
            values["trigger.callDepth"] = d;
        }

        if (parentRun is not null)
        {
            values["trigger.parentRun"] = parentRun;
        }

        return new BlockContext(WorkflowRunId.From(run), NodeId.From("call"), config, (JsonObject)config.DeepClone(), reference => values.GetValueOrDefault(reference)?.DeepClone());
    }

    private static Task<Aw.Kernel.Result<BlockResult>> Run(IGraphCallPort port, BlockContext context) =>
        new RunWorkflowBlock(port).Execute(context, TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task Depth_three_is_allowed_depth_four_is_refused_and_self_start_is_refused()
    {
        var config = new JsonObject { ["workflow"] = "triage" };

        var topLevel = await Run(new FlowFakes.ScriptedCallPort(), Caller(config));
        var third = await Run(new FlowFakes.ScriptedCallPort(), Caller(config, depth: 2));   // the callee is at depth 3
        var fourth = await Run(new FlowFakes.ScriptedCallPort(), Caller(config, depth: 3));   // the callee would be at depth 4
        var own = await Run(new FlowFakes.ScriptedCallPort(), Caller(new JsonObject { ["workflow"] = "nightly" }));
        var back = await Run(new FlowFakes.ScriptedCallPort(), Caller(new JsonObject { ["workflow"] = "nightly" }, depth: 1, parentRun: "nightly@2:4", run: "triage@1:100000001"));

        Assert.True(topLevel.IsSuccess);
        Assert.True(third.IsSuccess);
        Assert.Equal(SindriErrors.GraphCallDepth.Code, fourth.Error.Code);
        Assert.Equal(SindriErrors.GraphSelfStart.Code, own.Error.Code);
        Assert.Equal(SindriErrors.GraphSelfStart.Code, back.Error.Code);   // triage was called by nightly, so triage may not start nightly again
    }

    [Fact]
    public async Task The_profile_depth_decides_how_deep_a_call_may_go()
    {
        var config = new JsonObject { ["workflow"] = "triage" };

        var shallow = await Run(new FlowFakes.ScriptedCallPort(maxDepth: 1), Caller(config, depth: 1));
        var deep = await Run(new FlowFakes.ScriptedCallPort(maxDepth: 5), Caller(config, depth: 4));

        Assert.Equal(SindriErrors.GraphCallDepth.Code, shallow.Error.Code);
        Assert.True(deep.IsSuccess);
    }

    [Fact]
    public async Task A_call_starts_the_callee_with_the_payload_and_the_next_depth_and_does_not_wait_by_default()
    {
        var port = new FlowFakes.ScriptedCallPort();

        var result = await Run(port, Caller(new JsonObject { ["workflow"] = "triage", ["payload"] = new JsonObject { ["pr"] = 12 } }, depth: 1));

        var call = Assert.Single(port.Started);
        Assert.Equal(("nightly@2:5", "call", 0, "triage", 2), (call.Caller.Value, call.Node.Value, call.Index, call.Target.Value, call.Depth));
        Assert.Equal(12, call.Payload!["pr"]!.GetValue<int>());
        Assert.Empty(port.Waited);
        Assert.Equal("started", result.Value.Outputs["status"]!.GetValue<string>());
        Assert.Equal("triage@1:100000001", result.Value.Outputs["runId"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_item_index_of_a_for_each_reaches_the_call_so_each_item_is_its_own_run()
    {
        var port = new FlowFakes.ScriptedCallPort();
        var context = new BlockContext(WorkflowRunId.From("nightly@2:5"), NodeId.From("call"), new JsonObject { ["workflow"] = "triage" }, new JsonObject { ["workflow"] = "triage" },
            reference => reference == "itemIndex" ? JsonValue.Create(7) : null);

        _ = await Run(port, context);

        Assert.Equal(7, Assert.Single(port.Started).Index);
    }

    [Fact]
    public async Task With_wait_the_node_goes_on_when_the_called_run_ends_and_hands_on_its_outputs()
    {
        var port = new FlowFakes.ScriptedCallPort();

        var result = await Run(port, Caller(new JsonObject { ["workflow"] = "triage", ["wait"] = true, ["waitSeconds"] = 30 }));

        Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(port.Waited).Timeout);
        Assert.Equal("succeeded", result.Value.Outputs["status"]!.GetValue<string>());
        Assert.Equal(42, result.Value.Outputs["outputs"]!["answer"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_called_run_that_fails_fails_the_waiting_node_and_a_skipped_one_is_not_waited_for()
    {
        var failed = await Run(new FlowFakes.ScriptedCallPort { End = new("failed", Outputs: null, "boom") }, Caller(new JsonObject { ["workflow"] = "triage", ["wait"] = true }));
        var skippedPort = new FlowFakes.ScriptedCallPort { StartStatus = "skipped" };
        var skipped = await Run(skippedPort, Caller(new JsonObject { ["workflow"] = "triage", ["wait"] = true }));

        Assert.Contains("boom", failed.Error.Message, StringComparison.Ordinal);
        Assert.Equal("skipped", skipped.Value.Outputs["status"]!.GetValue<string>());
        Assert.Empty(skippedPort.Waited);
    }

    [Fact]
    public async Task A_workflow_name_that_is_not_a_slug_fails_the_node()
    {
        var result = await Run(new FlowFakes.ScriptedCallPort(), Caller(new JsonObject { ["workflow"] = "Not A Slug" }));

        Assert.Contains("slug", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_block_takes_a_workflow_and_optional_payload_wait_and_wait_seconds()
    {
        var block = new RunWorkflowBlock(new FlowFakes.ScriptedCallPort());

        Assert.Equal("run_workflow", block.Type);
        Assert.False(block.IsTrigger);
        Assert.Empty(block.Config.Check(new JsonObject { ["workflow"] = "triage", ["payload"] = new JsonObject(), ["wait"] = true, ["waitSeconds"] = 5 }));
        Assert.Contains(block.Config.Check([]), static p => p.Field == "workflow");
    }

    [Fact]
    public async Task The_workflow_call_trigger_hands_on_what_the_caller_sent()
    {
        var trigger = new WorkflowCallTrigger();
        var context = BlockFakes.Context([], new(StringComparer.Ordinal)
        {
            ["trigger.payload"] = new JsonObject { ["pr"] = 12 },
            ["trigger.parentRun"] = "nightly@2:5",
            ["trigger.callDepth"] = 2,
            ["trigger.by"] = "workflow:nightly",
        });

        var result = await trigger.Execute(context, TestContext.Current.CancellationToken);

        Assert.True(trigger.IsTrigger);
        Assert.Equal("workflow_call", trigger.Type);
        Assert.Equal(12, result.Value.Outputs["payload"]!["pr"]!.GetValue<int>());
        Assert.Equal("nightly@2:5", result.Value.Outputs["parentRun"]!.GetValue<string>());
        Assert.Equal(2, result.Value.Outputs["callDepth"]!.GetValue<int>());
    }
}
