using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A run's token budget (#3805): the run sums the tokens its 'prompt' and 'run_agent' nodes report; a prompt or run_agent node that would start once the total has
/// reached the budget fails with SND-GRAPH-134 and never asks the model. The spec's 'budget.tokens' wins over the profile's default; with neither there is no cap.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GraphTokenBudgetTests
{
    private static readonly JsonObject _ask = new() { ["prompt"] = "Say hi" };

    private static GraphSpec TwoPrompts(long? budget, string? onFailure = null)
    {
        var spec = GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("first", "prompt", config: (JsonObject)_ask.DeepClone()), GraphRig.Node("second", "prompt", onFailure, (JsonObject)_ask.DeepClone()), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "first"), GraphRig.Edge("first", "second"), GraphRig.Edge("second", "after")]);
        return budget is { } tokens ? Budgeted(spec, tokens) : spec;
    }

    private static GraphSpec Budgeted(GraphSpec spec, long tokens) => spec with { Budget = new GraphBudget(tokens) };

    private static async Task<(WorkflowRun Run, IReadOnlyDictionary<string, NodeRun> Nodes)> RunToEnd(GraphRig rig, GraphSpec spec, string trigger = """{"kind":"manual"}""")
    {
        var workflow = await rig.Define(spec);
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger);
        var run = await rig.Final(started.RunId, TimeSpan.FromMinutes(2));
        var rows = (await rig.Runs.NodeRuns(started.RunId, TestContext.Current.CancellationToken)).Value;
        return (run, rows.Where(static r => r.ItemIndex == 0).ToDictionary(static r => r.NodeId.Value, StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_prompt_that_would_start_over_the_budget_fails_with_SND_GRAPH_134_and_never_asks_the_model()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 120;

        var (run, nodes) = await RunToEnd(rig, TwoPrompts(budget: 100));

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["first"].Status);
        Assert.Equal(NodeRunStatus.Failed, nodes["second"].Status);
        Assert.StartsWith("SND-GRAPH-134: ", nodes["second"].Error, StringComparison.Ordinal);
        Assert.Contains("120", nodes["second"].Error, StringComparison.Ordinal);
        Assert.Contains("100", nodes["second"].Error, StringComparison.Ordinal);
        Assert.Equal(1, rig.Model.Calls);   // why: the refused node never reached the model
        Assert.Equal(NodeRunStatus.Skipped, nodes["after"].Status);
        Assert.Contains("SND-GRAPH-134", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_node_run_keeps_the_tokens_it_used_as_its_usage()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 120;

        var (_, nodes) = await RunToEnd(rig, TwoPrompts(budget: 100));

        Assert.Equal(120L, NodeRunUsage.Tokens(nodes["first"].Usage));
        Assert.Equal(0L, NodeRunUsage.Tokens(nodes["second"].Usage));
        Assert.Null(nodes["start"].Usage);
    }

    [Fact]
    public async Task With_no_budget_both_prompts_run()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 120;

        var (run, nodes) = await RunToEnd(rig, TwoPrompts(budget: null));

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["second"].Status);
        Assert.Equal(2, rig.Model.Calls);
    }

    [Fact]
    public async Task A_node_that_starts_under_the_budget_may_finish_over_it()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 60;

        var (run, nodes) = await RunToEnd(rig, TwoPrompts(budget: 100));

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);   // why: 'second' starts at 60 of 100 and ends at 120; nothing after it asks a model
        Assert.Equal(NodeRunStatus.Succeeded, nodes["second"].Status);
        Assert.Equal(2, rig.Model.Calls);
    }

    [Fact]
    public async Task The_profile_default_caps_a_run_whose_spec_sets_no_budget()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 120;
        rig.Budgets.Tokens = 100;

        var (run, nodes) = await RunToEnd(rig, TwoPrompts(budget: null));

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.StartsWith("SND-GRAPH-134: ", nodes["second"].Error, StringComparison.Ordinal);
        Assert.Equal(1, rig.Model.Calls);
    }

    [Fact]
    public async Task The_spec_budget_wins_over_the_profile_default()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 120;
        rig.Budgets.Tokens = 100;

        var (run, nodes) = await RunToEnd(rig, TwoPrompts(budget: 1000));

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["second"].Status);
        Assert.Equal(2, rig.Model.Calls);
    }

    [Fact]
    public async Task A_refused_node_with_on_failure_continue_lets_the_walk_go_on()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 120;

        var (run, nodes) = await RunToEnd(rig, TwoPrompts(budget: 100, onFailure: "continue"));

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(NodeRunStatus.Failed, nodes["second"].Status);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["after"].Status);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(1)]   // why: one item per batch, so the total crosses from one child workflow to the next through the parent
    public async Task A_loop_counts_every_item_and_refuses_the_first_item_that_starts_over_the_budget(int batchSize)
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 60;
        var spec = Budgeted(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}", ["batchSize"] = batchSize }),
                GraphRig.Node("ask", "prompt", onFailure: "skip-item", config: (JsonObject)_ask.DeepClone()), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "ask"), GraphRig.Edge("ask", "gather")]), 100);
        var trigger = new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray(1, 2, 3) }.ToJsonString();

        var (run, _) = await RunToEnd(rig, spec, trigger);
        var asks = await rig.ItemRuns(run.Id, "ask");
        var output = (await rig.NodeOutput(run.Id, "gather"))!;

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(NodeRunStatus.Succeeded, asks[1].Status);
        Assert.Equal(NodeRunStatus.Succeeded, asks[2].Status);   // why: it starts at 60 of 100
        Assert.Equal(NodeRunStatus.Failed, asks[3].Status);   // why: it would start at 120 of 100
        Assert.StartsWith("SND-GRAPH-134: ", asks[3].Error, StringComparison.Ordinal);
        Assert.Equal(2, rig.Model.Calls);
        Assert.Equal(1, output["skipped"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_node_after_the_loop_sees_the_tokens_its_items_used()
    {
        await using var rig = await GraphRig.StartAsync();
        rig.Model.TokensPerAnswer = 60;
        var spec = Budgeted(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
                GraphRig.Node("ask", "prompt", config: (JsonObject)_ask.DeepClone()), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" }),
                GraphRig.Node("last", "prompt", config: (JsonObject)_ask.DeepClone())],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "ask"), GraphRig.Edge("ask", "gather"), GraphRig.Edge("gather", "last")]), 100);
        var trigger = new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray(1, 2) }.ToJsonString();

        var (run, nodes) = await RunToEnd(rig, spec, trigger);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.StartsWith("SND-GRAPH-134: ", nodes["last"].Error, StringComparison.Ordinal);   // why: the two items used 120 of 100
        Assert.Equal(2, rig.Model.Calls);
    }
}
