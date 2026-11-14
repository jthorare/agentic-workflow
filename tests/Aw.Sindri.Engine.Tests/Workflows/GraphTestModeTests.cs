using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// P4a (#3197, spec 2026-11-12 §3.2 and §5.1): a test run walks the real graph but records every write and parks on nothing.
/// The walk gets 5 s on a server that skips time only when asked, so a run that really waits a day fails by timeout.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GraphTestModeTests
{
    private static readonly TimeSpan _fast = TimeSpan.FromSeconds(5);

    private static JsonObject Config(string key, string value) => new() { [key] = value };

    [Fact]
    public async Task A_test_run_of_wait_1d_finishes_and_records_would_wait()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("hold", "wait", config: Config("duration", "1d")), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "hold"), GraphRig.Edge("hold", "after")]));

        var started = await rig.TriggerTest(workflow, occurrence: 1);
        var run = await rig.Final(started.RunId, _fast);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(WorkflowRunMode.Test, run.Mode);
        var rows = (await rig.Runs.NodeRuns(started.RunId, TestContext.Current.CancellationToken)).Value;
        Assert.Contains("would wait 1d", rows.Single(static r => string.Equals(r.NodeId.Value, "hold", StringComparison.Ordinal)).Output, StringComparison.Ordinal);
        Assert.Equal([$"{rig.Slug}@1:1/after"], rig.Blocks.Log);
    }

    [Fact]
    public async Task A_test_run_of_ask_owner_takes_the_first_choice_without_asking()
    {
        await using var rig = await GraphRig.StartAsync();
        var ask = new JsonObject { ["question"] = "Ship the release?", ["choices"] = new JsonArray("yes", "no"), ["timeout"] = "1h" };
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("ask", "ask_owner", config: ask), GraphRig.Node("yes", "echo"), GraphRig.Node("no", "echo")],
            [GraphRig.Edge("start", "ask"), GraphRig.Edge("ask", "yes", "answered"), GraphRig.Edge("ask", "no", "timeout")]));

        var started = await rig.TriggerTest(workflow, occurrence: 1);
        var run = await rig.Final(started.RunId, _fast);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Empty(rig.Questions.Posted);
        Assert.Equal([$"{rig.Slug}@1:1/yes"], rig.Blocks.Log);
        Assert.Contains("yes", (await rig.NodeOutput(started.RunId, "ask"))?.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_test_run_of_notify_records_would_have_and_posts_nothing()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("tell", "notify", config: Config("title", "Main gate red"))],
            [GraphRig.Edge("start", "tell")]));

        var started = await rig.TriggerTest(workflow, occurrence: 1);
        var run = await rig.Final(started.RunId, _fast);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Empty(rig.Ports.Notified);
        var rows = (await rig.Runs.NodeRuns(started.RunId, TestContext.Current.CancellationToken)).Value;
        Assert.Contains("would have: notify 'Main gate red'", rows.Single(static r => string.Equals(r.NodeId.Value, "tell", StringComparison.Ordinal)).Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_test_run_of_create_item_records_would_have_and_makes_no_item()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("make", "create_item", config: Config("title", "Fix the drift"))],
            [GraphRig.Edge("start", "make")]));

        var started = await rig.TriggerTest(workflow, occurrence: 1);
        var run = await rig.Final(started.RunId, _fast);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Empty(rig.Ports.Created);
        Assert.Null(run.ItemId);
        var rows = (await rig.Runs.NodeRuns(started.RunId, TestContext.Current.CancellationToken)).Value;
        Assert.Contains("would have: create item 'Fix the drift'", rows.Single(static r => string.Equals(r.NodeId.Value, "make", StringComparison.Ordinal)).Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_test_run_of_only_new_remembers_nothing_so_a_live_run_still_sees_the_keys_as_new()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [
                GraphRig.Node("start", "manual"),
                GraphRig.Node("fresh", "only_new", config: new JsonObject { ["items"] = "{{trigger.issues}}", ["key"] = "id" }),
                GraphRig.Node("tell", "notify", config: Config("title", "New issues")),
            ],
            [GraphRig.Edge("start", "fresh"), GraphRig.Edge("fresh", "tell")]));
        const string issues = """{"kind":"manual","issues":[{"id":1},{"id":2}]}""";

        var test = await rig.Final((await rig.TriggerTest(workflow, occurrence: 1, issues)).RunId, _fast);
        var seenAfterTest = rig.Seen.Count;
        var live = await rig.Final((await rig.Trigger(workflow, occurrence: 2, GraphConcurrency.Unlimited, issues)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, test.Status);
        Assert.Equal(0, seenAfterTest);
        Assert.Equal(2, (await rig.NodeOutput(live.Id, "fresh"))!["count"]!.GetValue<int>());
    }
}
